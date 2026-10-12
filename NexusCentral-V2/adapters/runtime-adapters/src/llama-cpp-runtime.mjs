import { spawn } from "node:child_process";
import { EventEmitter } from "node:events";
import { accessSync, constants, realpathSync, statSync } from "node:fs";
import { createServer } from "node:net";
import { basename, dirname, extname, resolve } from "node:path";

const DEFAULT_CONTEXT_LENGTH = 16000;
const START_TIMEOUT_MS = 60_000;
const STOP_TIMEOUT_MS = 5_000;

/** Owns one local llama-server child process for the Nexus Node lifetime. */
export class LlamaCppRuntimeAdapter extends EventEmitter {
  constructor({ binaryPath = process.env.NEXUS_LLAMA_SERVER_PATH || "", fetchImpl = fetch, spawnImpl = spawn, startTimeoutMs = START_TIMEOUT_MS, stopTimeoutMs = STOP_TIMEOUT_MS } = {}) {
    super();
    this.binaryPath = String(binaryPath || "");
    this.fetchImpl = fetchImpl;
    this.spawnImpl = spawnImpl;
    this.startTimeoutMs = startTimeoutMs;
    this.stopTimeoutMs = stopTimeoutMs;
    this.child = null;
    this.runtime = null;
    this.starting = null;
    this.startController = null;
    this.stopping = null;
    this.stoppingChild = null;
  }

  status() {
    if (this.child && this.child.exitCode === null && this.child.signalCode === null && this.runtime) {
      const { modelPath: _modelPath, ...runtime } = this.runtime;
      return { running: true, ...runtime };
    }
    return { running: false, modelId: "", baseUrl: "", contextLength: 0 };
  }

  async start({ modelPath, contextLength = DEFAULT_CONTEXT_LENGTH } = {}) {
    if (this.starting) throw new Error("A llama.cpp runtime start is already in progress.");
    if (this.stopping) throw new Error("The managed llama.cpp runtime is still stopping. Retry after shutdown completes.");
    if (this.child && this.child.exitCode === null) throw new Error("The managed llama.cpp runtime is already running.");
    const model = assertRegularFile(modelPath, "GGUF model");
    const binary = assertExecutable(this.binaryPath);
    const context = Number(contextLength);
    if (!Number.isInteger(context) || context < DEFAULT_CONTEXT_LENGTH) throw new Error(`llama.cpp must be started with at least ${DEFAULT_CONTEXT_LENGTH} context tokens.`);
    this.startController = new AbortController();
    this.starting = this.#start({ model, binary, context, signal: this.startController.signal });
    try { return await this.starting; }
    finally { this.starting = null; this.startController = null; }
  }

  async #start({ model, binary, context, signal }) {
    const port = await findLoopbackPort();
    if (signal.aborted) throw new Error("llama.cpp startup was cancelled.");
    const modelId = basename(model, extname(model));
    const baseUrl = `http://127.0.0.1:${port}/v1`;
    const child = this.spawnImpl(binary, ["--model", model, "--ctx-size", String(context), "--host", "127.0.0.1", "--port", String(port), "--alias", modelId], {
      cwd: dirname(binary),
      windowsHide: true,
      stdio: ["ignore", "pipe", "pipe"],
      env: runtimeEnvironment(dirname(binary))
    });
    this.child = child;
    let output = "";
    const appendOutput = (chunk) => { output = `${output}${chunk.toString()}`.slice(-32_000); };
    child.stdout?.on("data", appendOutput);
    child.stderr?.on("data", appendOutput);
    let childExited = false;
    let resolveExit;
    const exited = new Promise((resolvePromise) => { resolveExit = resolvePromise; });
    child.once("exit", (code, signal) => {
      childExited = true;
      const result = { code, signal };
      resolveExit(result);
      const expected = this.stoppingChild === child;
      if (this.child === child) {
        this.child = null;
        if (this.runtime?.pid === child.pid) this.runtime = null;
      }
      if (expected) this.stoppingChild = null;
      else this.emit("exit", { code, signal, modelId });
    });
    let spawnError = null;
    let failSpawn;
    const spawnFailed = new Promise((resolveFailure) => { failSpawn = resolveFailure; });
    child.once("error", (error) => { spawnError = error; failSpawn(error); });
    try {
      const deadline = Date.now() + this.startTimeoutMs;
      let listening = false;
      while (Date.now() < deadline) {
        if (signal.aborted) throw new Error("llama.cpp startup was cancelled.");
        if (spawnError) throw new Error(`Unable to start llama-server: ${spawnError.message}`);
        if (childExited || child.exitCode !== null || child.signalCode !== null) throw new Error(`llama-server exited before readiness (${formatExit(await Promise.race([exited, Promise.resolve(null)]))}).`);
        // Upstream log wording varies by release: newer builds report
        // "llama_server: listening on http://..." while older ones used
        // "HTTP server is listening".
        if (/(?:HTTP server is listening|llama_server:\s*listening on http)/i.test(output)) listening = true;
        if (listening) {
          const health = await Promise.race([this.fetchImpl(`${baseUrl.replace(/\/v1$/, "")}/health`, { signal: AbortSignal.timeout(1000) }).catch(() => null), exited.then((result) => { throw new Error(`llama-server exited during readiness (${formatExit(result)}).`); })]);
          if (health?.ok && !childExited && child.exitCode === null && child.signalCode === null) {
            const modelResponse = await Promise.race([this.fetchImpl(`${baseUrl}/models`, { signal: AbortSignal.timeout(1000) }).catch(() => null), exited.then((result) => { throw new Error(`llama-server exited during model verification (${formatExit(result)}).`); })]);
            const models = modelResponse?.ok ? (await modelResponse.json().catch(() => null))?.data || [] : [];
            if (!models.some((item) => String(item.id) === modelId)) throw new Error("The managed llama-server reported ready without the configured model identity.");
            if (childExited || child.exitCode !== null || child.signalCode !== null) throw new Error("llama-server exited while completing readiness checks.");
            this.runtime = { modelId, modelPath: model, baseUrl, contextLength: context, pid: child.pid };
            const { modelPath: _modelPath, ...runtime } = this.runtime;
            return { ...runtime, running: true };
          }
        }
        const delay = Math.max(1, Math.min(200, deadline - Date.now()));
        await Promise.race([new Promise((resolvePromise) => setTimeout(resolvePromise, delay)), exited.then((result) => { throw new Error(`llama-server exited before readiness (${formatExit(result)}).`); }), spawnFailed.then((error) => { throw new Error(`Unable to start llama-server: ${error.message}`); })]);
      }
      throw new Error("llama-server did not become ready before the startup deadline.");
    } catch (error) {
      await terminateChild(child, this.stopTimeoutMs).catch(() => {});
      if (this.child === child) {
        this.child = null;
        if (this.runtime?.pid === child.pid) this.runtime = null;
      }
      throw new Error(`${error.message}${output ? ` ${output.trim().slice(-1000)}` : ""}`);
    }
  }

  async stop({ expectedPid } = {}) {
    if (this.stopping) return { ...(await this.stopping), alreadyStopping: true };
    if (expectedPid !== undefined && (!this.child || this.child.pid !== expectedPid)) return { stopped: true, wasRunning: false, skipped: true };
    if (this.starting && this.startController) {
      if (this.child) this.stoppingChild = this.child;
      this.startController.abort();
      await this.starting.catch(() => {});
      if (!this.child) return { stopped: true, wasRunning: false, cancelledStart: true };
    }
    const child = this.child;
    if (!child) return { stopped: true, wasRunning: false };
    if (expectedPid !== undefined && child.pid !== expectedPid) return { stopped: true, wasRunning: false, skipped: true };
    const runtime = this.runtime?.pid === child.pid ? this.runtime : null;
    this.stoppingChild = child;
    const stopping = (async () => {
      try { await terminateChild(child, this.stopTimeoutMs); }
      finally {
        if (this.child === child) {
          this.child = null;
          if (this.runtime?.pid === child.pid) this.runtime = null;
        }
        if (this.stoppingChild === child) this.stoppingChild = null;
      }
      return { stopped: true, wasRunning: Boolean(runtime), modelId: runtime?.modelId || "" };
    })();
    this.stopping = stopping;
    try { return await stopping; }
    finally { if (this.stopping === stopping) this.stopping = null; }
  }

  stopIfOwned(pid) { return this.stop({ expectedPid: pid }); }

  async close() { await this.stop(); }
}

function assertRegularFile(path, label) {
  if (!path || typeof path !== "string") throw new Error(`${label} path is required.`);
  let resolved;
  try { resolved = realpathSync(resolve(path)); }
  catch { throw new Error(`${label} path does not exist.`); }
  try { if (!statSync(resolved).isFile()) throw new Error(); }
  catch { throw new Error(`${label} path must be a regular file.`); }
  return resolved;
}

function assertExecutable(path) {
  if (!path) throw new Error("Configure the pinned llama-server runtime first (NEXUS_LLAMA_SERVER_PATH).");
  const resolved = assertRegularFile(path, "llama-server executable");
  try { accessSync(resolved, constants.X_OK); }
  catch { throw new Error("The configured llama-server path is not executable."); }
  return resolved;
}

function runtimeEnvironment(binaryDirectory) {
  const env = { ...process.env };
  const libraryKey = process.platform === "darwin" ? "DYLD_LIBRARY_PATH" : "LD_LIBRARY_PATH";
  env[libraryKey] = [binaryDirectory, env[libraryKey]].filter(Boolean).join(process.platform === "win32" ? ";" : ":");
  return env;
}

function findLoopbackPort() {
  return new Promise((resolvePromise, reject) => {
    const server = createServer();
    server.once("error", reject);
    server.listen({ host: "127.0.0.1", port: 0, exclusive: true }, () => {
      const address = server.address();
      server.close((error) => error ? reject(error) : resolvePromise(address.port));
    });
  });
}

async function terminateChild(child, timeoutMs) {
  if (child.exitCode !== null || child.signalCode !== null || !child.pid) return;
  const exited = new Promise((resolvePromise) => child.once("exit", resolvePromise));
  child.kill("SIGTERM");
  const graceful = await Promise.race([exited.then(() => true), new Promise((resolvePromise) => setTimeout(() => resolvePromise(false), timeoutMs))]);
  if (!graceful && child.exitCode === null) {
    child.kill("SIGKILL");
    await Promise.race([exited, new Promise((resolvePromise) => setTimeout(resolvePromise, 1000))]);
  }
}

function formatExit(exit) {
  if (!exit) return "unknown status";
  return exit.signal ? `signal ${exit.signal}` : `code ${exit.code}`;
}
