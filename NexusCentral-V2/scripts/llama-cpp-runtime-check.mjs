import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import { PassThrough } from "node:stream";
import { chmodSync, mkdtempSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { LlamaCppRuntimeAdapter } from "../adapters/runtime-adapters/index.mjs";

const temp = mkdtempSync(join(tmpdir(), "nexuscentral-llama-adapter-check-"));
const binary = join(temp, "llama-server");
const model = join(temp, "model.gguf");
writeFileSync(binary, "fixture executable");
writeFileSync(model, "fixture model");
chmodSync(binary, 0o755);

class FakeChild extends EventEmitter {
  constructor({ pid = 43210, exitDelayMs = 0 } = {}) { super(); this.pid = pid; this.exitDelayMs = exitDelayMs; this.killCount = 0; this.exitCode = null; this.signalCode = null; this.killed = false; this.stdout = new PassThrough(); this.stderr = new PassThrough(); }
  kill(signal) {
    this.killCount += 1;
    this.killed = true;
    const exit = () => { this.exitCode = 0; this.signalCode = signal; this.emit("exit", 0, signal); };
    if (this.exitDelayMs) setTimeout(exit, this.exitDelayMs);
    else setImmediate(exit);
    return true;
  }
}

try {
  let spawnCount = 0;
  const fetchImpl = async (url) => {
    assert.match(url, /^http:\/\/127\.0\.0\.1:\d+\//, "managed endpoint must bind loopback only");
    if (url.endsWith("/health")) return new Response("{}", { status: 200 });
    if (url.endsWith("/v1/models")) return new Response(JSON.stringify({ data: [{ id: "model" }] }), { status: 200 });
    return new Response("not found", { status: 404 });
  };
  const runtime = new LlamaCppRuntimeAdapter({ binaryPath: binary, fetchImpl, spawnImpl: () => {
    spawnCount += 1;
    const child = new FakeChild();
    setImmediate(() => child.stderr.write("main: HTTP server is listening, hostname: 127.0.0.1\n"));
    return child;
  }, startTimeoutMs: 500, stopTimeoutMs: 100 });
  await assert.rejects(runtime.start({ modelPath: join(temp, "missing.gguf") }), /GGUF model path does not exist/);
  assert.equal(spawnCount, 0, "missing models must fail before process launch");
  const missingBinary = new LlamaCppRuntimeAdapter({ binaryPath: join(temp, "missing-server") });
  await assert.rejects(missingBinary.start({ modelPath: model }), /llama-server executable path does not exist/);
  const started = await runtime.start({ modelPath: model, contextLength: 16000 });
  assert.equal(started.running, true);
  assert.equal(started.contextLength, 16000);
  assert.equal("modelPath" in started, false, "host paths are not returned as runtime state");
  assert.equal(runtime.status().running, true);
  await assert.rejects(runtime.start({ modelPath: model }), /already running/);
  const stopped = await runtime.stop();
  assert.equal(stopped.stopped, true);
  assert.equal(runtime.status().running, false);

  let cleanupChild;
  const failed = new LlamaCppRuntimeAdapter({ binaryPath: binary, fetchImpl: async (url) => url.endsWith("/health") ? new Response("{}", { status: 200 }) : new Response(JSON.stringify({ data: [{ id: "other-model" }] }), { status: 200 }), spawnImpl: () => {
    cleanupChild = new FakeChild();
    setImmediate(() => cleanupChild.stderr.write("main: HTTP server is listening, hostname: 127.0.0.1\n"));
    return cleanupChild;
  }, startTimeoutMs: 500, stopTimeoutMs: 100 });
  await assert.rejects(failed.start({ modelPath: model }), /without the configured model identity/);
  assert.equal(cleanupChild.killed, true, "failed readiness must terminate the owned child");
  assert.equal(failed.status().running, false);

  let timeoutChild;
  const timeout = new LlamaCppRuntimeAdapter({ binaryPath: binary, fetchImpl, spawnImpl: () => (timeoutChild = new FakeChild()), startTimeoutMs: 30, stopTimeoutMs: 100 });
  await assert.rejects(timeout.start({ modelPath: model }), /startup deadline/);
  assert.equal(timeoutChild.killed, true, "startup timeout must terminate the owned child");
  assert.equal(timeout.status().running, false);

  let cancelledChild;
  const cancelled = new LlamaCppRuntimeAdapter({ binaryPath: binary, fetchImpl, spawnImpl: () => (cancelledChild = new FakeChild()), startTimeoutMs: 2000, stopTimeoutMs: 100 });
  const starting = cancelled.start({ modelPath: model });
  await new Promise((resolvePromise) => setTimeout(resolvePromise, 10));
  const cancelledResult = await cancelled.stop();
  await assert.rejects(starting, /startup was cancelled/);
  assert.equal(cancelledResult.cancelledStart, true);
  assert.equal(cancelledChild.killed, true, "canceling startup must terminate its owned child");
  assert.equal(cancelled.status().running, false);

  let exitDuringVerify;
  const exitedDuringVerify = new LlamaCppRuntimeAdapter({ binaryPath: binary, fetchImpl: async (url) => {
    if (url.endsWith("/health")) return new Response("{}", { status: 200 });
    exitDuringVerify.exitCode = null;
    exitDuringVerify.signalCode = "SIGKILL";
    exitDuringVerify.emit("exit", null, "SIGKILL");
    return new Response(JSON.stringify({ data: [{ id: "model" }] }), { status: 200 });
  }, spawnImpl: () => {
    exitDuringVerify = new FakeChild();
    setImmediate(() => exitDuringVerify.stderr.write("main: HTTP server is listening, hostname: 127.0.0.1\n"));
    return exitDuringVerify;
  }, startTimeoutMs: 500, stopTimeoutMs: 100 });
  await assert.rejects(exitedDuringVerify.start({ modelPath: model }), /exited (during model verification|while completing readiness)/);
  assert.equal(exitedDuringVerify.status().running, false, "an early child exit cannot be reported ready");

  const delayedChildren = [];
  const serialized = new LlamaCppRuntimeAdapter({ binaryPath: binary, fetchImpl, spawnImpl: () => {
    const child = new FakeChild({ pid: 44000 + delayedChildren.length, exitDelayMs: 80 });
    delayedChildren.push(child);
    setImmediate(() => child.stderr.write("main: HTTP server is listening, hostname: 127.0.0.1\n"));
    return child;
  }, startTimeoutMs: 500, stopTimeoutMs: 500 });
  await serialized.start({ modelPath: model });
  const firstStop = serialized.stop();
  const secondStop = serialized.stop();
  await assert.rejects(serialized.start({ modelPath: model }), /still stopping/);
  const [firstStopResult, secondStopResult] = await Promise.all([firstStop, secondStop]);
  assert.equal(firstStopResult.stopped, true);
  assert.equal(secondStopResult.alreadyStopping, true);
  assert.equal(delayedChildren[0].killCount, 1, "concurrent stops must signal an owned process once");
  await serialized.start({ modelPath: model });
  assert.equal(serialized.status().pid, delayedChildren[1].pid, "a replacement can start after the prior process exits");
  await serialized.close();
  assert.equal(serialized.status().running, false);
  console.log(JSON.stringify({ status: "passed", cases: ["missing model", "missing binary", "loopback readiness", "repeated start", "stop owned child", "wrong model identity cleanup", "startup timeout cleanup", "startup cancellation cleanup", "exit during model verification", "start serialized with stop", "concurrent stop is idempotent"] }, null, 2));
}
finally { rmSync(temp, { recursive: true, force: true }); }
