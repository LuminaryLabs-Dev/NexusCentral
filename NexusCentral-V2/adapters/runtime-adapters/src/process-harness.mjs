import { spawn } from "node:child_process";
import { existsSync, mkdtempSync, realpathSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { isAbsolute, relative, resolve, sep } from "node:path";

const SAFE_SANDBOXES = new Set(["read-only", "workspace-write"]);
const SAFE_LOCAL_PROVIDERS = new Set(["lmstudio", "ollama"]);

export class ProcessHarnessAdapter {
  buildCodexArgs(profile, stage, configuration, workspaceRoot, prompt, runtime = {}) {
    if (profile.executor !== "codex-cli" || !profile.stages.includes(stage)) throw new Error(`Harness cannot run stage ${stage}.`);
    for (const [name, value] of Object.entries(configuration)) if (isSet(value) && !["sandbox", "validationCommands", "simulatorTarget"].includes(name) && !profile.allowedOptions.includes(name)) throw new Error(`Harness option ${name} is not allowed by this profile.`);
    const sandbox = stage === "act" ? configuration.sandbox || profile.defaultSandbox : "read-only";
    if (!SAFE_SANDBOXES.has(sandbox) || !profile.allowedSandboxes.includes(sandbox)) throw new Error("Harness sandbox is not allowed.");
    const args = ["exec", "--json", "--ephemeral", "--skip-git-repo-check", "--sandbox", sandbox, "--cd", realpathSync(workspaceRoot)];
    const gateway = runtime.gateway;
    if (gateway && (configuration.profile || configuration.oss || configuration.localProvider)) throw new Error("Codex gateway runs cannot override NexusCentral provider routing.");
    if (configuration.model || gateway) args.push("--model", safeName(configuration.model || gateway.modelId || "nexuscentral-routed", "model"));
    if (!gateway && configuration.profile) args.push("--profile", safeName(configuration.profile, "profile"));
    if (!gateway && configuration.oss) args.push("--oss");
    if (!gateway && configuration.localProvider) {
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
    const args = this.buildCodexArgs(profile, stage, configuration, options.workspaceRoot, prompt, { gateway: options.gateway });
    const codexHome = options.gateway ? createGatewayHome(options.gateway) : process.env.CODEX_HOME || "";
    try {
      const result = await spawnCaptured("codex", args, { cwd: realpathSync(options.workspaceRoot), signal: options.signal, timeoutMs: options.timeoutMs || 10 * 60_000, onOutput: options.onOutput, env: { CODEX_HOME: codexHome, NEXUS_CENTRAL_GATEWAY_TOKEN: options.gateway?.token || "" } });
      if (options.stdoutPath) writeFileSync(options.stdoutPath, result.stdout);
      if (options.stderrPath) writeFileSync(options.stderrPath, result.stderr);
      return { stage, args: redactArgs(args), output: finalMessage(result.stdout), ...result };
    } catch (error) {
      if (options.stdoutPath) writeFileSync(options.stdoutPath, error.stdout || "");
      if (options.stderrPath) writeFileSync(options.stderrPath, error.stderr || error.message);
      throw error;
    } finally { if (options.gateway) rmSync(codexHome, { recursive: true, force: true }); }
  }

  async discover() {
    const version = await spawnCaptured("codex", ["--version"], { timeoutMs: 30_000 });
    return { version: version.stdout.trim(), supported: { sandboxes: [...SAFE_SANDBOXES], localProviders: [...SAFE_LOCAL_PROVIDERS], flags: ["model", "profile", "sandbox", "oss", "localProvider", "images", "outputSchema", "strictConfig"] } };
  }
}

async function spawnCaptured(executable, args, { cwd, signal, timeoutMs, onOutput, env = {} } = {}) {
  const child = spawn(executable, args, { cwd, stdio: ["ignore", "pipe", "pipe"], env: { PATH: process.env.PATH, HOME: process.env.HOME, ...env } });
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

function createGatewayHome(gateway) {
  const root = mkdtempSync(resolve(tmpdir(), "nexuscentral-codex-"));
  const baseUrl = tomlString(gateway.baseUrl);
  const model = tomlString(gateway.modelId || "nexuscentral-routed");
  writeFileSync(resolve(root, "config.toml"), `model_provider = "nexuscentral"\nmodel = "${model}"\n\n[model_providers.nexuscentral]\nname = "NexusCentral Router"\nbase_url = "${baseUrl}"\nenv_key = "NEXUS_CENTRAL_GATEWAY_TOKEN"\nwire_api = "chat"\nrequest_max_retries = 0\nrequires_openai_auth = false\n`);
  return root;
}

function tomlString(value) { return String(value).replaceAll("\\", "\\\\").replaceAll('"', '\\"').replaceAll("\n", "\\n"); }
