import assert from "node:assert/strict";
import { existsSync, mkdirSync, mkdtempSync, readFileSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { commandEnvelope } from "@nexuscentral/contracts";
import { Libp2pMeshAdapter, MemorySecretStore, PeerJsRoomAdapter, ProviderAdapterRegistry } from "@nexuscentral/runtime-adapters";
import { NexusNode } from "../services/nexus-node/src/nexus-node.mjs";
import { LanServer } from "../services/nexus-node/src/lan-server.mjs";
import { PeerRoomBroker } from "../services/peer-broker/src/peer-broker.mjs";

const temp = mkdtempSync(join(tmpdir(), "nexuscentral-v2-"));
const workspace = join(temp, "workspace");
const databasePath = join(temp, "nexuscentral.sqlite");
const secrets = new MemorySecretStore();
writeFileSync(join(temp, "seed"), "seed");
mkdirSync(workspace);
writeFileSync(join(workspace, "README.md"), "proof workspace");
writeFileSync(join(workspace, "validate.mjs"), "import { readFileSync } from 'node:fs'; if (readFileSync('feature.txt', 'utf8') !== 'fixed') process.exit(1);\n");
const outside = join(temp, "outside");
mkdirSync(outside);
symlinkSync(outside, join(workspace, "escape"));

const sse = "data: {\"choices\":[{\"delta\":{\"content\":\"host \"}}]}\n\ndata: {\"choices\":[{\"delta\":{\"content\":\"routed\"}}]}\n\ndata: [DONE]\n\n";
const fetchImpl = async (url) => {
  if (String(url).endsWith("/models")) return new Response(JSON.stringify({ data: [{ id: "qwen-proof" }] }), { status: 200, headers: { "Content-Type": "application/json" } });
  if (String(url).endsWith("/chat/completions")) return new Response(sse, { status: 200, headers: { "Content-Type": "text/event-stream" } });
  throw new Error(`Unexpected mock URL: ${url}`);
};

const protocolSecrets = new MemorySecretStore();
protocolSecrets.set("anthropic", "anthropic-proof"); protocolSecrets.set("google", "google-proof");
const protocolFetch = async (url, options = {}) => {
  const target = String(url);
  if (target.includes("anthropic.test/models")) return Response.json({ data: [{ id: "claude-proof", display_name: "Claude Proof" }] });
  if (target.includes("anthropic.test/messages")) return new Response('data: {"type":"content_block_delta","delta":{"text":"anthropic routed"}}\n\n', { headers: { "Content-Type": "text/event-stream" } });
  if (target.includes("google.test/models?") || target.endsWith("google.test/models")) return Response.json({ models: [{ name: "models/gemini-proof", displayName: "Gemini Proof" }] });
  if (target.includes("google.test/models/gemini-proof:streamGenerateContent")) return new Response('data: {"candidates":[{"content":{"parts":[{"text":"google routed"}]}}]}\n\n', { headers: { "Content-Type": "text/event-stream" } });
  throw new Error(`Unexpected protocol mock URL: ${target} ${options.method || "GET"}`);
};
const protocolRegistry = new ProviderAdapterRegistry({ secretStore: protocolSecrets, fetchImpl: protocolFetch });
const anthropicProfile = { id: "anthropic-proof", kind: "anthropic", protocol: "anthropic-messages", baseUrl: "https://anthropic.test", modelId: "claude-proof", secretRef: "anthropic" };
const googleProfile = { id: "google-proof", kind: "google-native", protocol: "google-generate", baseUrl: "https://google.test", modelId: "gemini-proof", secretRef: "google" };
assert.equal((await protocolRegistry.probe(anthropicProfile))[0].id, "claude-proof");
assert.equal((await protocolRegistry.probe(googleProfile))[0].id, "gemini-proof");
const protocolChunks = [];
await protocolRegistry.streamChat(anthropicProfile, { messages: [{ role: "user", content: "proof" }], maxOutputTokens: 16 }, (chunk) => protocolChunks.push(chunk));
await protocolRegistry.streamChat(googleProfile, { messages: [{ role: "user", content: "proof" }], maxOutputTokens: 16 }, (chunk) => protocolChunks.push(chunk));
assert.deepEqual(protocolChunks, ["anthropic routed", "google routed"]);
const admin = { actorId: "local", scopes: ["admin"] };
const command = (node, name, payload = {}) => node.execute(commandEnvelope(name, payload), admin);
let actAttempt = 0;
const fakeHarness = {
  discover: async () => ({ version: "codex-cli integration-fake", supported: { sandboxes: ["read-only", "workspace-write"], localProviders: ["lmstudio", "ollama"], flags: ["model", "profile", "sandbox"] } }),
  async runCodexStage(_profile, stage, _configuration, _prompt, options) {
    if (stage === "act") { actAttempt += 1; writeFileSync(join(options.workspaceRoot, "feature.txt"), actAttempt === 1 ? "broken" : "fixed"); }
    writeFileSync(options.stdoutPath, `${JSON.stringify({ type: "item.completed", item: { type: "agent_message", text: `${stage} complete` } })}\n`);
    writeFileSync(options.stderrPath, "");
    return { stage, args: ["exec", "--json", "<prompt>"], output: `${stage} complete`, stdout: "", stderr: "", exitCode: 0 };
  }
};
const node = new NexusNode({ databasePath, secretStore: secrets, fetchImpl, harnessProcess: fakeHarness });

await command(node, "workspace.open", { path: workspace, workspaceId: "proof-workspace" });
assert.throws(() => node.filesystem.write("proof-workspace", "escape/blocked.txt", "blocked"), /symbolic links/);
const retryJob = await command(node, "inference.submit", { messages: [{ role: "user", content: "wait for a route" }], contextLength: 16000, maxOutputTokens: 16000 });
await new Promise((resolvePromise) => setTimeout(resolvePromise, 30));
assert.equal(node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === retryJob.id).status, "retry");
const canceledRun = await command(node, "inference.submit", { messages: [{ role: "user", content: "cancel while waiting" }], contextLength: 16000, maxOutputTokens: 16 });
await new Promise((resolvePromise) => setTimeout(resolvePromise, 30));
await command(node, "runs.cancel", { id: canceledRun.id });
assert.equal((await command(node, "runs.get", { id: canceledRun.id })).status, "canceled");
const savedProvider = await command(node, "providers.save", { id: "proof-provider", kind: "custom", name: "Proof Provider", baseUrl: "http://provider.test/v1", modelId: "qwen-proof", apiKey: "integration-secret" });
assert.equal("secretRef" in savedProvider, false);
await command(node, "providers.test", { id: "proof-provider" });
await command(node, "providers.set-share-policy", { id: "proof-provider", enabled: true, concurrencyLimit: 2, tokensPerMinute: 32000 });
assert.equal(node.meshCapabilities().capabilities.some((item) => item.providerId === "proof-provider" && item.concurrencyLimit === 2), true);
for (let attempt = 0; attempt < 100; attempt += 1) {
  const resumed = node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === retryJob.id);
  if (resumed.status === "completed") break;
  await new Promise((resolvePromise) => setTimeout(resolvePromise, 10));
}
assert.equal(node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === retryJob.id).status, "completed");
const job = await command(node, "inference.submit", { messages: [{ role: "user", content: "prove host routing" }], contextLength: 16000, maxOutputTokens: 32 });
let completed;
for (let attempt = 0; attempt < 100; attempt += 1) {
  completed = node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === job.id);
  if (["completed", "failed"].includes(completed.status)) break;
  await new Promise((resolvePromise) => setTimeout(resolvePromise, 10));
}
assert.equal(completed.status, "completed");
assert.equal(completed.progress.map((item) => item.content).join(""), "host routed");
assert.equal(completed.route.id, "proof-provider");
const canonicalRun = await command(node, "runs.submit", { kind: "inference", messages: [{ role: "user", content: "prove canonical run submission" }], contextLength: 16000, maxOutputTokens: 32 });
for (let attempt = 0; attempt < 100 && (await command(node, "runs.get", { id: canonicalRun.id })).status !== "completed"; attempt += 1) await new Promise((resolvePromise) => setTimeout(resolvePromise, 10));
assert.equal((await command(node, "runs.get", { id: canonicalRun.id })).status, "completed");
const publicState = await command(node, "system.state");
assert.equal("secretRef" in publicState.providers.profiles[0], false);
assert.equal(publicState.harnesses.profiles.some((item) => item.id === "coding-harness"), true);
assert.equal("invocations" in publicState.harnesses.profiles.find((item) => item.id === "coding-harness"), false);
assert.equal("pairings" in publicState.sessions, false);
assert.equal(publicState.audit.entries.length > 0, true);

await command(node, "harnesses.select", { id: "coding-harness" });
await command(node, "harnesses.configure", { id: "coding-harness", sandbox: "workspace-write", validationCommands: [{ command: "node", args: ["validate.mjs"] }] });
const experiment = await command(node, "lab.submit", { task: "Create feature.txt containing fixed" });
let experimentState;
for (let attempt = 0; attempt < 300; attempt += 1) {
  experimentState = node.engine.n.centralExperiments.getSnapshot().experiments.find((item) => item.id === experiment.id);
  if (["review", "failed"].includes(experimentState.status)) break;
  await new Promise((resolvePromise) => setTimeout(resolvePromise, 10));
}
assert.equal(experimentState.status, "review");
assert.equal(experimentState.attempt, 2);
assert.equal(experimentState.validation.passed, true);
assert.equal(experimentState.scope.id, `run:${experiment.id}`);
assert.deepEqual(experimentState.sandbox, { id: `simspace:${experiment.id}`, kind: "workspace-copy", mode: "workspace-write", lifecycle: "run-scoped" });
assert.equal((await command(node, "runs.get", { id: experiment.id })).status, "approval_required");
assert.equal(existsSync(join(workspace, "feature.txt")), false);
assert.equal(experimentState.diff.files.some((file) => file.includes("feature.txt")), true);
for (let attempt = 0; attempt < 100 && existsSync(node.experimentAdapter.location(experiment.id).workspace); attempt += 1) await new Promise((resolvePromise) => setTimeout(resolvePromise, 10));
assert.equal(existsSync(node.experimentAdapter.location(experiment.id).workspace), false);
const experimentArtifacts = await command(node, "artifacts.list", { runId: experiment.id });
assert.equal(experimentArtifacts.some((item) => item.path.endsWith("manifest.json")), true);
assert.equal(experimentArtifacts.some((item) => item.path.endsWith("changes.patch")), true);
assert.equal((await command(node, "artifacts.read", { id: experimentArtifacts.find((item) => item.path.endsWith("manifest.json")).id })).encoding, "utf8");
await command(node, "experiments.approve", { id: experiment.id });
assert.equal(existsSync(join(workspace, "feature.txt")), false);
await command(node, "experiments.promote", { id: experiment.id });
assert.equal(readFileSync(join(workspace, "feature.txt"), "utf8"), "fixed");
assert.equal(node.engine.n.centralWorkspace.getSnapshot().selected.revision, 1);
assert.equal((await command(node, "runs.get", { id: experiment.id })).status, "completed");
const decisionArtifacts = await command(node, "artifacts.list", { runId: experiment.id });
assert.equal(decisionArtifacts.some((item) => item.path.endsWith("decisions/approval.json")), true);
assert.equal(decisionArtifacts.some((item) => item.path.endsWith("decisions/promotion.json")), true);

node.engine.n.centralArtifacts.record({ id: "artifact:escape-proof", hash: "", kind: "output", jobId: experiment.id, runId: experiment.id, path: "../seed", bytes: 4, mediaType: "text/plain", createdAt: Date.now(), requestId: "artifact-escape-proof" });
await assert.rejects(() => command(node, "artifacts.read", { id: "artifact:escape-proof" }), /escapes/);

await command(node, "providers.save", { id: "local-provider", kind: "lm-studio", name: "Local Proof Provider", baseUrl: "http://provider.test/v1", modelId: "qwen-proof" });
await command(node, "providers.test", { id: "local-provider" });
node.engine.n.centralModelRuntime.register({ id: "lm-studio:runtime-proof", kind: "lm-studio", modelId: "qwen-proof", contextLength: 16000, status: "ready", requestId: "runtime-provider-id-proof" });
const runtimeRun = await command(node, "inference.submit", { messages: [{ role: "user", content: "prove runtime provider resolution" }], contextLength: 16000, maxOutputTokens: 32 });
let runtimeJob;
for (let attempt = 0; attempt < 100; attempt += 1) { runtimeJob = node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === runtimeRun.id); if (["completed", "failed"].includes(runtimeJob.status)) break; await new Promise((resolvePromise) => setTimeout(resolvePromise, 10)); }
assert.equal(runtimeJob.status, "completed");
assert.equal(runtimeJob.route.id, "lm-studio:runtime-proof");

const pairing = await command(node, "lan.create-pairing");
const session = await node.execute(commandEnvelope("lan.pair", { code: pairing.code, clientName: "Proof Browser" }), { actorId: "anonymous", scopes: [] });
assert.deepEqual(session.scopes, ["view", "inference.contribute"]);
await node.execute(commandEnvelope("browser.register-runtime", { id: "browser-proof", modelId: "onnx-proof", taskType: "tensor", fingerprint: "abcdef", executionProvider: "wasm", contextLength: 16000, smokePassed: true }), { actorId: session.id, scopes: session.scopes });
assert.equal(node.engine.n.centralModelRuntime.getSnapshot().runtimes.at(-1).sessionId, session.id);
node.engine.n.centralJobs.enqueue({ id: "interrupted-harness", idempotencyKey: "interrupted-harness", kind: "harness", payload: {}, createdAt: Date.now(), requestId: "interrupted-harness:enqueue" });
node.engine.n.centralJobs.claim({ id: "interrupted-harness", route: { id: "proof-harness", kind: "harness" }, ownerId: "proof-node", expiresAt: Date.now() + 60_000, requestId: "interrupted-harness:claim" });
node.persist();

const server = new LanServer({ node, staticRoot: resolve("apps/web/out"), port: 0 });
const address = await server.start();
assert.equal((await fetch(`${address.url}/api/v1/capabilities`)).status, 200);
assert.equal((await command(node, "service.status")).supported, process.platform === "darwin");
const crossOrigin = await fetch(`${address.url}/api/v1/commands`, { method: "POST", headers: { Origin: "https://malicious.example", "Content-Type": "application/json" }, body: JSON.stringify(commandEnvelope("system.state", {})) });
assert.equal(crossOrigin.status, 403);
const gateway = server.issueGatewayAccess({ runId: experiment.id, allowedTools: ["workspace.read"], expiresAt: Date.now() + 60_000 });
assert.equal((await fetch(`${address.url}/v1/chat/completions`, { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ model: "nexuscentral-routed", messages: [{ role: "user", content: "unauthorized" }], stream: true }) })).status, 401);
const gatewayResponse = await fetch(`${address.url}/v1/chat/completions`, { method: "POST", headers: { Authorization: `Bearer ${gateway.token}`, "Content-Type": "application/json" }, body: JSON.stringify({ model: "nexuscentral-routed", messages: [{ role: "user", content: "gateway proof" }], tools: [{ type: "function", function: { name: "workspace_read", parameters: { type: "object" } } }], stream: true }) });
assert.equal(gatewayResponse.status, 200);
assert.equal((await gatewayResponse.text()).includes("host "), true);
const gatewayJob = node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.kind === "gateway-inference" && item.scope?.parentRunId === experiment.id);
assert.equal(gatewayJob.status, "completed");
const gatewayArtifacts = await command(node, "artifacts.list", { runId: experiment.id });
const downloadable = gatewayArtifacts.find((item) => item.path.endsWith(".stream.sse"));
assert.ok(downloadable);
assert.equal((await fetch(`${address.url}/api/v1/artifacts/${encodeURIComponent(downloadable.id)}`)).status, 200);
await server.close();
await node.close();

const restored = new NexusNode({ databasePath, secretStore: secrets, fetchImpl });
assert.equal(restored.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === job.id).status, "completed");
assert.equal(restored.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === "interrupted-harness").status, "failed");
assert.equal(restored.engine.n.centralWorkspace.getSnapshot().selected.id, "proof-workspace");
await restored.close();

const meshA = new Libp2pMeshAdapter({ secretStore: new MemorySecretStore() });
const meshB = new Libp2pMeshAdapter({ secretStore: new MemorySecretStore() });
meshB.setRequestHandler(async (request) => ({ action: request.action, payload: request.payload }));
await meshA.start({ lanCode: "integration-lan-code" });
await meshB.start({ lanCode: "integration-lan-code" });
for (let attempt = 0; attempt < 40 && meshA.peers.size === 0; attempt += 1) await new Promise((resolvePromise) => setTimeout(resolvePromise, 100));
assert.equal(meshA.peers.size > 0, true);
const meshResult = await meshA.request([...meshA.peers.keys()][0], "mesh.ping", { proof: true });
assert.deepEqual(meshResult, { action: "mesh.ping", payload: { proof: true } });
await meshA.stop(); await meshB.stop();

const broker = new PeerRoomBroker({ port: 0 });
const brokerAddress = await broker.start();
const roomA = new PeerJsRoomAdapter({ nodeId: "room-node-a" });
const roomB = new PeerJsRoomAdapter({ nodeId: "room-node-b" });
const isolatedRoom = new PeerJsRoomAdapter({ nodeId: "room-node-isolated" });
const roomChunks = [];
roomB.setRequestHandler(async (request, _peer, stream) => { await stream.onChunk("peer "); await stream.onChunk("routed"); return { action: request.action, accepted: true }; });
await roomA.start({ roomCode: "integration-room", brokerUrl: brokerAddress.url, iceServers: [] });
await roomB.start({ roomCode: "integration-room", brokerUrl: brokerAddress.url, iceServers: [] });
await isolatedRoom.start({ roomCode: "different-room", brokerUrl: brokerAddress.url, iceServers: [] });
for (let attempt = 0; attempt < 40 && roomA.peers.size === 0; attempt += 1) { await roomA.discover(); await new Promise((resolvePromise) => setTimeout(resolvePromise, 50)); }
await isolatedRoom.discover();
assert.equal(roomA.peers.size, 1);
assert.equal(isolatedRoom.peers.size, 0);
assert.equal(JSON.stringify(roomA.status()).includes("integration-room"), false);
const roomResult = await roomA.request([...roomA.peers.keys()][0], "mesh.inference", { proof: true }, { onChunk: (content) => roomChunks.push(content) });
assert.deepEqual(roomResult, { action: "mesh.inference", accepted: true });
assert.equal(roomChunks.join(""), "peer routed");
await roomA.stop(); await roomB.stop(); await isolatedRoom.stop(); await broker.close();

rmSync(temp, { recursive: true, force: true });
await new Promise((resolvePromise) => process.stdout.write(`${JSON.stringify({ status: "passed", providerStream: "host routed", specializedProviderStreams: protocolChunks, providerShareOptIn: true, routeRetryResumed: true, disposableExperiment: true, boundedRepair: actAttempt === 2, approvalBeforePromotion: true, interruptedJobRecovered: true, persistedJob: true, pairingScopes: session.scopes, browserRuntimeLease: true, crossOriginRejected: true, workspaceRestored: true, libp2pDirectRequest: true, peerJsDirectRequest: roomChunks.join(""), peerJsRoomIsolation: true }, null, 2)}\n`, resolvePromise));
// The native Node WebRTC binding may retain an internal worker after every
// PeerJS connection is explicitly closed. This standalone proof owns the
// process, so exit only after assertions, teardown, cleanup, and output flush.
process.exit(0);
