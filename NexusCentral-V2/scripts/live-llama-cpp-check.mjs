import assert from "node:assert/strict";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { commandEnvelope } from "@nexuscentral/contracts";
import { MemorySecretStore } from "@nexuscentral/runtime-adapters";
import { NexusNode } from "../services/nexus-node/src/nexus-node.mjs";

const modelPath = process.env.NEXUS_LLAMA_MODEL_PATH;
if (!modelPath) throw new Error("Set NEXUS_LLAMA_MODEL_PATH to a local GGUF file before running the live llama.cpp check.");
if (!process.env.NEXUS_LLAMA_SERVER_PATH) throw new Error("Set NEXUS_LLAMA_SERVER_PATH to the pinned llama-server executable before running the live llama.cpp check.");
const temp = mkdtempSync(join(tmpdir(), "nexuscentral-llama-live-"));
const node = new NexusNode({ databasePath: join(temp, "state.sqlite"), secretStore: new MemorySecretStore() });
const admin = { actorId: "live-llama-proof", scopes: ["admin"] };
const command = (name, payload = {}) => node.execute(commandEnvelope(name, payload), admin);
const startedAt = new Date().toISOString();
let nodeClosed = false;

try {
  await assert.rejects(command("models.start-llamacpp", { modelPath: join(temp, "missing.gguf") }), /GGUF model path does not exist/);
  const runtime = await command("models.start-llamacpp", { modelPath: resolve(modelPath) });
  assert.equal(runtime.running, true);
  assert.equal(runtime.contextLength, 16000);
  assert.match(runtime.baseUrl, /^http:\/\/127\.0\.0\.1:\d+\/v1$/);
  await command("providers.select", { id: runtime.providerId });
  const submitted = await command("inference.submit", { providerId: runtime.providerId, allowMesh: false, messages: [{ role: "user", content: "Reply with exactly: NexusCentral llama.cpp works." }], contextLength: 16000, maxOutputTokens: 64 });
  let job;
  for (let attempt = 0; attempt < 900; attempt += 1) {
    job = node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === submitted.id);
    if (["completed", "failed", "cancelled", "retry"].includes(job?.status)) break;
    await new Promise((resolvePromise) => setTimeout(resolvePromise, 100));
  }
  assert.equal(job?.status, "completed", job?.error || `Inference ended as ${job?.status || "missing"}.`);
  const response = job.progress.map((chunk) => chunk.content).join("").trim();
  assert.match(response.toLowerCase(), /nexuscentral/);
  const modelRuntime = node.engine.n.centralModelRuntime.getSnapshot().runtimes.find((item) => item.id === `llamacpp-managed:${runtime.providerId}`);
  assert.equal(modelRuntime?.contextLength, 16000);
  await node.close();
  nodeClosed = true;
  assert.equal(isProcessAlive(runtime.pid), false, "Nexus Node shutdown must terminate its owned llama-server child");
  console.log(JSON.stringify({ status: "passed", startedAt, completedAt: new Date().toISOString(), jobId: job.id, providerId: runtime.providerId, modelId: runtime.modelId, runtimePid: runtime.pid, route: job.route?.id, contextLength: 16000, response, childStoppedOnShutdown: true }, null, 2));
}
finally {
  if (!nodeClosed) await node.close();
  rmSync(temp, { recursive: true, force: true });
}

function isProcessAlive(pid) {
  if (!pid) return false;
  try { process.kill(pid, 0); return true; }
  catch (error) { if (error.code === "ESRCH") return false; throw error; }
}
