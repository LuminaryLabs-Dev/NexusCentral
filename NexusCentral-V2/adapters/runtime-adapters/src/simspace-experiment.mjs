import { spawn, spawnSync } from "node:child_process";
import { createHash, randomUUID } from "node:crypto";
import { appendFileSync, copyFileSync, existsSync, lstatSync, mkdirSync, readFileSync, readdirSync, realpathSync, rmSync, writeFileSync } from "node:fs";
import { dirname, isAbsolute, join, relative, resolve, sep } from "node:path";

const EXCLUDED = new Set([".git", "node_modules", ".next", "out", "release", ".package-stage", ".simspaces", ".nexus-simulator"]);
const VALIDATORS = new Set(["npm", "node", "dotnet", "cargo"]);

export class SimSpaceExperimentAdapter {
  constructor({ runRoot, simulatorCli = process.env.NEXUS_SIMULATOR_CLI || "", maxWorkspaceBytes = 1_000_000_000 }) {
    this.runRoot = resolve(runRoot);
    this.simulatorCli = simulatorCli;
    this.maxWorkspaceBytes = maxWorkspaceBytes;
    mkdirSync(this.runRoot, { recursive: true });
  }

  location(id) { const root = resolve(this.runRoot, id); assertInside(this.runRoot, root); return { root, workspace: join(root, "workspace") }; }

  prepare({ id, sourceRoot, manifest }) {
    const root = resolve(this.runRoot, id); rmSync(root, { recursive: true, force: true });
    const workspace = join(root, "workspace");
    for (const folder of [workspace, join(root, "inputs"), join(root, "outputs"), join(root, "decisions"), join(root, "validation")]) mkdirSync(folder, { recursive: true });
    copyTree(realpathSync(sourceRoot), workspace, { bytes: 0, maxBytes: this.maxWorkspaceBytes });
    run("git", ["init", "-q"], workspace); run("git", ["config", "user.email", "nexuscentral@local"], workspace); run("git", ["config", "user.name", "NexusCentral SimSpace"], workspace); run("git", ["add", "-A"], workspace); run("git", ["commit", "-qm", "SimSpace baseline"], workspace);
    const baselineHash = digestTree(sourceRoot);
    writeFileSync(join(root, "manifest.json"), `${JSON.stringify({ ...manifest, id, baselineHash, createdAt: Date.now() }, null, 2)}\n`);
    this.event(root, { type: "experiment.prepared", baselineHash });
    return { root, workspace, baselineHash };
  }

  event(root, event) { appendFileSync(join(root, "events.jsonl"), `${JSON.stringify({ eventId: randomUUID(), timestamp: Date.now(), ...event })}\n`); }
  artifact(root, ...parts) { const target = resolve(root, ...parts); assertInside(root, target); mkdirSync(dirname(target), { recursive: true }); return target; }

  async validate(root, workspace, commands = [], simulatorTarget = "") {
    const results = [];
    for (const spec of commands) {
      const args = assertValidatorSpec(workspace, spec);
      const result = await spawnAsync(spec.command, args, workspace, Math.min(Number(spec.timeoutMs || 120_000), 10 * 60_000));
      const log = this.artifact(root, "validation", `${results.length + 1}-${spec.command}.log`); writeFileSync(log, `${result.stdout}\n${result.stderr}`);
      results.push({ command: spec.command, args: spec.args, exitCode: result.exitCode, artifact: relative(root, log), passed: result.exitCode === 0 });
    }
    const simulator = simulatorTarget ? await this.runSimulator(root, workspace, simulatorTarget) : null;
    const report = { passed: results.every((item) => item.passed) && (!simulator || simulator.passed), repairable: results.some((item) => !item.passed), commands: results, simulator };
    writeFileSync(this.artifact(root, "validation-report.json"), `${JSON.stringify(report, null, 2)}\n`); this.event(root, { type: "experiment.validated", passed: report.passed });
    return report;
  }

  async runSimulator(root, workspace, logicalTarget) {
    if (!this.simulatorCli || !existsSync(this.simulatorCli)) throw new Error("NexusSimulator CLI is unavailable.");
    const target = resolve(workspace, logicalTarget); assertInside(workspace, target);
    const result = await spawnAsync("node", [this.simulatorCli, "validate", target, "--tool", "interaction.proof"], root, 5 * 60_000);
    const artifact = this.artifact(root, "validation", "nexus-simulator.log"); writeFileSync(artifact, `${result.stdout}\n${result.stderr}`);
    const reportPath = result.stdout.match(/"reportPath"\s*:\s*"([^"]+)"/)?.[1]?.replaceAll("\\\\", "\\") || "";
    const retained = [];
    if (reportPath && existsSync(reportPath)) {
      const report = JSON.parse(readFileSync(reportPath, "utf8"));
      const retainedReport = this.artifact(root, "validation", "nexus-simulator-report.json"); copyFileSync(reportPath, retainedReport); retained.push(relative(root, retainedReport));
      for (const source of report.artifacts || []) if (existsSync(source)) { const target = this.artifact(root, "validation", `nexus-${retained.length}-${source.split(sep).at(-1)}`); copyFileSync(source, target); retained.push(relative(root, target)); }
    }
    return { passed: result.exitCode === 0, exitCode: result.exitCode, artifact: relative(root, artifact), retained };
  }

  diff(root, workspace) {
    run("git", ["add", "-N", "."], workspace);
    const patch = run("git", ["diff", "--binary", "HEAD"], workspace).stdout;
    const status = run("git", ["status", "--short"], workspace).stdout.trim().split("\n").filter(Boolean);
    const path = this.artifact(root, "outputs", "changes.patch"); writeFileSync(path, patch);
    return { hash: createHash("sha256").update(patch).digest("hex"), artifact: relative(root, path), files: status, bytes: Buffer.byteLength(patch), empty: !patch };
  }

  cleanup(id) {
    const { root, workspace } = this.location(id);
    rmSync(workspace, { recursive: true, force: true });
    this.event(root, { type: "sandbox.cleaned", sandbox: "workspace-copy" });
  }

  promote({ sourceRoot, experimentRoot, expectedBaselineHash, expectedDiffHash }) {
    if (digestTree(sourceRoot) !== expectedBaselineHash) throw new Error("Source workspace changed after the experiment started.");
    const patchPath = resolve(experimentRoot, "outputs", "changes.patch"); assertInside(experimentRoot, patchPath);
    const hash = createHash("sha256").update(readFileSync(patchPath)).digest("hex");
    if (hash !== expectedDiffHash) throw new Error("Experiment diff no longer matches its approval.");
    run("git", ["apply", "--check", "--binary", patchPath], sourceRoot); run("git", ["apply", "--binary", patchPath], sourceRoot);
    this.event(experimentRoot, { type: "experiment.promoted", diffHash: hash }); return { diffHash: hash };
  }
}

function copyTree(source, target, budget) {
  const metadata = lstatSync(source); if (metadata.isSymbolicLink()) return;
  if (metadata.isDirectory()) { mkdirSync(target, { recursive: true }); for (const name of readdirSync(source)) if (!EXCLUDED.has(name) && !name.startsWith(".nexuscentral-data")) copyTree(join(source, name), join(target, name), budget); return; }
  if (metadata.isFile()) {
    budget.bytes += metadata.size;
    if (budget.bytes > budget.maxBytes) throw new Error("Workspace exceeds the SimSpace copy budget.");
    copyFileSync(source, target);
  }
}

function assertValidatorSpec(workspace, spec) {
  if (!VALIDATORS.has(spec.command) || !Array.isArray(spec.args)) throw new Error("Validation command is outside the safe executable contract.");
  const args = spec.args.map(String);
  if (args.some((item) => /[;&|`\n]/.test(item) || isAbsolute(item) || item === ".." || item.startsWith(`..${sep}`))) throw new Error("Validation arguments escape the safe command contract.");

  if (spec.command === "npm") {
    const script = args[0] === "test" && args.length === 1 ? "test" : args[0] === "run" && args.length === 2 && /^[a-zA-Z0-9:_-]+$/.test(args[1]) ? args[1] : "";
    if (!script) throw new Error("npm validation is limited to one package script.");
    const baseline = JSON.parse(run("git", ["show", "HEAD:package.json"], workspace).stdout);
    const current = JSON.parse(readFileSync(join(workspace, "package.json"), "utf8"));
    if (!baseline.scripts?.[script] || baseline.scripts[script] !== current.scripts?.[script]) throw new Error("The selected npm validation script differs from the SimSpace baseline.");
  }

  if (spec.command === "node") {
    if (args.length !== 1 || !/\.(cjs|mjs|js)$/.test(args[0])) throw new Error("node validation requires one baseline script path.");
    const currentPath = resolve(workspace, args[0]); assertInside(workspace, currentPath);
    const baseline = run("git", ["show", `HEAD:${args[0]}`], workspace).stdout;
    if (!existsSync(currentPath) || baseline !== readFileSync(currentPath, "utf8")) throw new Error("The selected node validation script differs from the SimSpace baseline.");
  }

  if (spec.command === "dotnet" && (!new Set(["build", "test"]).has(args[0]) || args.slice(1).some((item) => item.startsWith("-")))) throw new Error("dotnet validation is limited to build or test and a relative project path.");
  if (spec.command === "cargo" && (args.length !== 1 || !new Set(["build", "check", "test"]).has(args[0]))) throw new Error("cargo validation is limited to build, check, or test.");
  return args;
}

function digestTree(root) {
  const canonical = realpathSync(root); const hash = createHash("sha256");
  const walk = (directory) => { for (const name of readdirSync(directory).sort()) { if (EXCLUDED.has(name) || name.startsWith(".nexuscentral-data")) continue; const path = join(directory, name); const stat = lstatSync(path); if (stat.isSymbolicLink()) continue; hash.update(relative(canonical, path)); if (stat.isDirectory()) walk(path); else if (stat.isFile()) hash.update(readFileSync(path)); } };
  walk(canonical); return hash.digest("hex");
}

function assertInside(root, target) { const rel = relative(resolve(root), resolve(target)); if (rel === ".." || rel.startsWith(`..${sep}`) || isAbsolute(rel)) throw new Error("SimSpace path escapes its root."); }
function run(executable, args, cwd) { const result = spawnSync(executable, args, { cwd, encoding: "utf8", env: { PATH: process.env.PATH, HOME: process.env.HOME } }); if (result.status !== 0) throw new Error(result.stderr || `${executable} exited with ${result.status}.`); return { stdout: result.stdout || "", stderr: result.stderr || "", exitCode: result.status }; }
function spawnAsync(executable, args, cwd, timeoutMs) { return new Promise((resolvePromise, reject) => { const child = spawn(executable, args, { cwd, stdio: ["ignore", "pipe", "pipe"], env: { PATH: process.env.PATH, HOME: process.env.HOME } }); let stdout = ""; let stderr = ""; child.stdout.on("data", (chunk) => { stdout += chunk; }); child.stderr.on("data", (chunk) => { stderr += chunk; }); child.once("error", reject); const timeout = setTimeout(() => child.kill("SIGTERM"), timeoutMs); child.once("close", (exitCode) => { clearTimeout(timeout); resolvePromise({ stdout, stderr, exitCode }); }); }); }
