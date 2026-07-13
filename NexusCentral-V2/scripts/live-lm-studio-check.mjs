import assert from "node:assert/strict";
import { mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { commandEnvelope } from "@nexuscentral/contracts";
import { MemorySecretStore } from "@nexuscentral/runtime-adapters";
import { NexusNode } from "../services/nexus-node/src/nexus-node.mjs";

const baseUrl = process.env.LM_STUDIO_URL || "http://10.0.0.137:1234/v1";
const modelId = process.env.LM_STUDIO_MODEL || "qwen3.5-4b";
const temp = mkdtempSync(join(tmpdir(), "nexuscentral-lm-live-"));
const node = new NexusNode({ databasePath: join(temp, "state.sqlite"), secretStore: new MemorySecretStore() });
const admin = { actorId: "live-proof", scopes: ["admin"] };
const command = (name, payload = {}) => node.execute(commandEnvelope(name, payload), admin);

try {
  await command("providers.save", { id: "live-lm-studio", kind: "lm-studio", name: "LM Studio LAN", baseUrl, modelId });
  const health = await command("providers.test", { id: "live-lm-studio" });
  assert.equal(health.models.some((model) => model.id === modelId), true, `${modelId} is not advertised by ${baseUrl}.`);
  const submitted = await command("inference.submit", { messages: [{ role: "user", content: "Reply with exactly: nexus-v2-live" }], contextLength: 16000, maxOutputTokens: 16000 });
  let job;
  for (let attempt = 0; attempt < 600; attempt += 1) {
    job = node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === submitted.id);
    if (["completed", "failed", "cancelled", "retry"].includes(job.status)) break;
    await new Promise((resolve) => setTimeout(resolve, 100));
  }
  assert.equal(job.status, "completed", job.error || `Live job ended as ${job.status}.`);
  const text = job.progress.map((chunk) => chunk.content).join("").trim();
  assert.match(text, /nexus-v2-live/i);
  console.log(JSON.stringify({ status: "passed", baseUrl, modelId, contextLength: 16000, route: job.route.id, response: text }, null, 2));
} finally {
  await node.close();
  rmSync(temp, { recursive: true, force: true });
}
