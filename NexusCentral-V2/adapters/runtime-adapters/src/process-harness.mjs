import { spawn } from "node:child_process";
import { existsSync, realpathSync, writeFileSync } from "node:fs";
import { isAbsolute, relative, resolve, sep } from "node:path";

const SAFE_SANDBOXES = new Set(["read-only", "workspace-write"]);
const SAFE_LOCAL_PROVIDERS = new Set(["lmstudio", "ollama"]);

export class ProcessHarnessAdapter {
  buildCodexArgs(profile, stage, configuration, workspaceRoot, prompt) {
    if (profile.executor !== "codex-cli" || !profile.stages.includes(stage)) throw new Error(`Harness cannot run stage ${stage}.`);
    for (const [name, value] of Object.entries(configuration)) if (isSet(value) && !["sandbox", "validationCommands", "simulatorTarget"].includes(name) && !profile.allowedOptions.includes(name)) throw new Error(`Harness option ${name} is not allowed by this profile.`);
    const sandbox = stage === "act" ? configuration.sandbox || profile.defaultSandbox : "read-only";
    if (!SAFE_SANDBOXES.has(sandbox) || !profile.allowedSandboxes.includes(sandbox)) throw new Error("Harness sandbox is not allowed.");
    const args = ["exec", "--json", "--ephemeral", "--skip-git-repo-check", "--sandbox", sandbox, "--cd", realpathSync(workspaceRoot)];
    if (configuration.model) args.push("--model", safeName(configuration.model, "model"));
    if (configuration.profile) args.push("--profile", safeName(configuration.profile, "profile"));
    if (configuration.oss) args.push("--oss");
    if (configuration.localProvider) {
      if (!configuration.oss || !SAFE_LOCAL_PROVIDERS.has(configuration.localProvider)) throw new Error("Local provider requires --oss and must be lmstudio or ollama.");
      args.push("--local-provider", configuration.localProvider);
    }
    for (const image of configuration.images || []) args.push("--image", inside(workspaceRoot, image));
    if (configuration.outputSchema) args.push("--output-schema", inside(workspaceRoot, configuration.outputSchema));
    if (configuration.strictConfig) args.push("--strict-config");
    args.push(prompt);
    return args;
  }

  async runCodexStage(profile, stage, configuration, prompt, options = {}) {
    const args = this.buildCodexArgs(profile, stage, configuration, options.workspaceRoot, prompt);
    try {
      const result = await spawnCaptured("codex", args, { cwd: realpathSync(options.workspaceRoot), signal: options.signal, timeoutMs: options.timeoutMs || 10 * 60_000, onOutput: options.onOutput });
      if (options.stdoutPath) writeFileSync(options.stdoutPath, result.stdout);
      if (options.stderrPath) writeFileSync(options.stderrPath, result.stderr);
      return { stage, args: redactArgs(args), output: finalMessage(result.stdout), ...result };
    } catch (error) {
      if (options.stdoutPath) writeFileSync(options.stdoutPath, error.stdout || "");
      if (options.stderrPath) writeFileSync(options.stderrPath, error.stderr || error.message);
      throw error;
    }
  }

  async discover() {
    const version = await spawnCaptured("codex", ["--version"], { timeoutMs: 10_000 });
    return { version: version.stdout.trim(), supported: { sandboxes: [...SAFE_SANDBOXES], localProviders: [...SAFE_LOCAL_PROVIDERS], flags: ["model", "profile", "sandbox", "oss", "localProvider", "images", "outputSchema", "strictConfig"] } };
  }
}

async function spawnCaptured(executable, args, { cwd, signal, timeoutMs, onOutput } = {}) {
  const child = spawn(executable, args, { cwd, stdio: ["ignore", "pipe", "pipe"], env: { PATH: process.env.PATH, HOME: process.env.HOME, CODEX_HOME: process.env.CODEX_HOME || "" } });
  const timeout = setTimeout(() => child.kill("SIGTERM"), timeoutMs || 10 * 60_000);
  const abort = () => child.kill("SIGTERM"); signal?.addEventListener("abort", abort, { once: true });
  let stdout = ""; let stderr = "";
  child.stdout.on("data", (chunk) => { stdout += chunk; onOutput?.(chunk.toString()); });
  child.stderr.on("data", (chunk) => { stderr += chunk; });
  const exitCode = await new Promise((resolvePromise, reject) => { child.once("error", reject); child.once("close", resolvePromise); });
  clearTimeout(timeout); signal?.removeEventListener("abort", abort);
  if (exitCode !== 0) throw Object.assign(new Error(stderr.trim() || `Harness exited with ${exitCode}.`), { stdout, stderr, exitCode });
  return { stdout, stderr, exitCode };
}

function inside(root, logicalPath) {
  const target = resolve(root, logicalPath);
  const rel = relative(realpathSync(root), existsSync(target) ? realpathSync(target) : target);
  if (rel === ".." || rel.startsWith(`..${sep}`) || isAbsolute(rel)) throw new Error("Codex attachment path escapes the SimSpace.");
  return target;
}

function safeName(value, label) {
  const result = String(value);
  if (!/^[A-Za-z0-9._:/-]{1,160}$/.test(result)) throw new Error(`Invalid Codex ${label}.`);
  return result;
}

function redactArgs(args) { return args.map((value, index) => index === args.length - 1 ? "<prompt>" : value); }
function isSet(value) { return Array.isArray(value) ? value.length > 0 : value !== "" && value !== false && value !== null && value !== undefined; }
function finalMessage(stdout) {
  const lines = stdout.trim().split("\n").filter(Boolean);
  for (let index = lines.length - 1; index >= 0; index -= 1) {
    try { const event = JSON.parse(lines[index]); if (event.type === "item.completed" && event.item?.type === "agent_message") return event.item.text || ""; } catch (_error) {}
  }
  return lines.at(-1) || "";
}
