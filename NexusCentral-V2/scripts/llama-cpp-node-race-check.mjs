import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { commandEnvelope } from "@nexuscentral/contracts";
import { MemorySecretStore } from "@nexuscentral/runtime-adapters";
import { NexusNode } from "../services/nexus-node/src/nexus-node.mjs";

const temp = mkdtempSync(join(tmpdir(), "nexuscentral-llama-race-check-"));
const pending = new Map();
const probeQueue = [];
let port = 61000;

class MockLlamaRuntime extends EventEmitter {
  constructor() { super(); this.current = null; this.nextPid = 51000; }
  async start({ modelPath }) {
    if (this.current) throw new Error("already running");
    port += 1;
    this.current = { running: true, modelId: modelPath.includes("model-c") ? "model-c" : "model-a", modelPath, baseUrl: `http://127.0.0.1:${port}/v1`, contextLength: 16000, pid: ++this.nextPid };
    return { ...this.current };
  }
  status() { return this.current ? { ...this.current } : { running: false, pid: undefined, modelId: "", baseUrl: "", contextLength: 0 }; }
  async stop({ expectedPid } = {}) {
    if (expectedPid !== undefined && this.current?.pid !== expectedPid) return { stopped: true, skipped: true, wasRunning: false };
    const wasRunning = Boolean(this.current);
    this.current = null;
    return { stopped: true, wasRunning };
  }
  stopIfOwned(pid) { return this.stop({ expectedPid: pid }); }
  async close() { return this.stop(); }
  crash() {
    const previous = this.current;
    this.current = null;
    if (previous) this.emit("exit", { code: null, signal: "SIGKILL", modelId: previous.modelId });
  }
}

const fetchImpl = (url) => {
  const actualPort = Number(new URL(url).port);
  return new Promise((resolvePromise, reject) => {
    pending.set(actualPort, { resolve: resolvePromise, reject });
    probeQueue.push(actualPort);
  });
};
const llamaRuntime = new MockLlamaRuntime();
const node = new NexusNode({ databasePath: join(temp, "state.sqlite"), secretStore: new MemorySecretStore(), fetchImpl, llamaCppRuntime: llamaRuntime });
const admin = { actorId: "llama-race-check", scopes: ["admin"] };
const command = (name, payload = {}) => node.execute(commandEnvelope(name, payload), admin);

try {
  const startA = command("models.start-llamacpp", { modelPath: "/sandbox/model-a.gguf" });
  const probePortA = await waitForProbe();
  await assert.rejects(command("models.start-llamacpp", { modelPath: "/sandbox/model-duplicate.gguf" }), /already being verified/);
  assert.equal(llamaRuntime.status().pid, 51001, "a duplicate start must not invalidate the active startup");
  await command("models.stop-llamacpp");
  const startB = command("models.start-llamacpp", { modelPath: "/sandbox/model-b.gguf" });
  const probePortB = await waitForProbe();
  pending.get(probePortA).reject(new Error("stale probe A rejected"));
  await assert.rejects(startA, /stale probe A rejected/);
  assert.equal(llamaRuntime.status().pid, 51002, "a stale start failure must not stop the replacement child");
  pending.get(probePortB).resolve(Response.json({ data: [{ id: "model-a" }] }));
  const startedB = await startB;
  assert.equal(startedB.pid, 51002);
  assert.equal(node.engine.n.centralProviders.getSnapshot().profiles.find((item) => item.id === startedB.providerId).health, "healthy");

  await command("models.stop-llamacpp");
  const startC = command("models.start-llamacpp", { modelPath: "/sandbox/model-c.gguf" });
  const probePortC = await waitForProbe();
  llamaRuntime.crash();
  pending.get(probePortC).resolve(Response.json({ data: [{ id: "model-c" }] }));
  await assert.rejects(startC, /stopped during provider verification/);
  assert.equal(llamaRuntime.status().running, false);
  assert.equal(node.engine.n.centralProviders.getSnapshot().profiles.find((item) => item.id === "managed-llama-cpp").health, "inactive");
  assert.equal(node.engine.n.centralModelRuntime.getSnapshot().runtimes.some((item) => item.id === "llamacpp-managed:managed-llama-cpp"), false);
  console.log(JSON.stringify({ status: "passed", cases: ["stop during provider probe", "stale probe cannot stop replacement", "unexpected child exit invalidates pending registration"] }, null, 2));
}
finally {
  for (const request of pending.values()) request.reject(new Error("test cleanup"));
  await node.close();
  rmSync(temp, { recursive: true, force: true });
}

async function waitForProbe() {
  for (let attempt = 0; attempt < 200; attempt += 1) {
    const entry = probeQueue.shift();
    if (entry) return entry;
    await new Promise((resolvePromise) => setTimeout(resolvePromise, 5));
  }
  throw new Error("Provider probe did not start.");
}
