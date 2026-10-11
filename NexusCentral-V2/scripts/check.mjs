import assert from "node:assert/strict";
import { readFileSync, readdirSync } from "node:fs";
import { join, resolve } from "node:path";
import { createEngine } from "nexusengine";
import { createCentralRuntime, getCentralGraphSnapshot } from "../packages/composition/index.mjs";
import { createCentralPolicyKit } from "../kits/policy-kit/index.mjs";
import { HarnessExecutorRegistry, ProcessHarnessAdapter } from "@nexuscentral/runtime-adapters";

const expectedPaths = [
  "n:central", "n:central:artifacts", "n:central:audit", "n:central:experiments", "n:central:harnesses", "n:central:inference", "n:central:jobs",
  "n:central:lab", "n:central:lan-access", "n:central:mesh-membership", "n:central:model-catalog", "n:central:model-runtime",
  "n:central:node", "n:central:policy", "n:central:proof", "n:central:providers", "n:central:routing", "n:central:workspace",
  "n:central:workflows", "n:central:workspace-transfer"
];

const engine = createCentralRuntime({ node: { nodeId: "node-check", displayName: "Check Node" } });
const graph = getCentralGraphSnapshot(engine);
assert.deepEqual(graph.paths.map((entry) => entry.path).sort(), expectedPaths.sort());
assert.equal(graph.central.readiness, "ready");

const denied = engine.n.centralPolicy.evaluate({ peerId: "peer-beta", action: "mesh.inference", requestId: "request-denied" });
assert.equal(denied.decision, "deny");
engine.n.centralPolicy.grant({ peerId: "peer-beta", action: "mesh.inference", grantId: "grant-peer-beta-inference" });
assert.equal(engine.n.centralPolicy.evaluate({ peerId: "peer-beta", action: "mesh.inference", requestId: "request-allowed" }).decision, "allow");

engine.n.centralWorkspace.select({ workspaceId: "workspace-1", name: "Sandbox", revision: 2, requestId: "workspace-select" });
engine.n.centralWorkspace.acquireWriter({ jobId: "job-write", holderId: "local", expectedRevision: 2, expiresAt: 1000, requestId: "lease-acquire" });
assert.throws(() => engine.n.centralWorkspace.acquireWriter({ jobId: "job-other", holderId: "peer", expectedRevision: 2, expiresAt: 1000, requestId: "lease-conflict" }), /held/);
engine.n.centralWorkspace.releaseWriter({ jobId: "job-write", incrementRevision: true, requestId: "lease-release" });
assert.equal(engine.n.centralWorkspace.getSnapshot().selected.revision, 3);

engine.n.centralProviders.upsert({ id: "lm", kind: "lm-studio", name: "LM Studio", baseUrl: "http://127.0.0.1:1234/v1", modelId: "qwen", requestId: "provider-save" });
engine.n.centralProviders.observeHealth({ id: "lm", health: "healthy", models: [{ id: "qwen" }], requestId: "provider-health" });
engine.n.centralProviders.setSharePolicy({ id: "lm", enabled: true, concurrencyLimit: 2, tokensPerMinute: 32000, requestId: "provider-share" });
assert.deepEqual(engine.n.centralProviders.getSnapshot().profiles[0].sharePolicy, { enabled: true, concurrencyLimit: 2, tokensPerMinute: 32000 });
engine.n.centralMeshMembership.join({ transport: "peerjs", networkId: "derived-network-id", requestId: "mesh-room-join" });
assert.equal(engine.n.centralMeshMembership.getSnapshot().networks.peerjs.networkId, "derived-network-id");
engine.n.centralModelCatalog.register({ id: "qwen", name: "Qwen", format: "gguf", contextLength: 16000, requestId: "model-register" });
engine.n.centralModelRuntime.register({ id: "browser-runtime", kind: "browser-onnx", modelId: "web-model", sessionId: "session-1", fingerprint: "abc", executionProvider: "webgpu", contextLength: 16000, expiresAt: 5000, requestId: "runtime-register" });
const route = engine.n.centralRouting.select({ requestId: "route-1", contextLength: 16000, candidates: [{ id: "remote", kind: "provider", healthy: true, contextLength: 16000, latencyMs: 50 }, { id: "lm", kind: "provider", healthy: true, local: true, contextLength: 16000, latencyMs: 5 }] });
assert.equal(route.selected.id, "lm");
const preferredRoute = engine.n.centralRouting.select({ requestId: "route-preferred", contextLength: 16000, preferredId: "remote", candidates: [{ id: "remote", kind: "provider", healthy: true, contextLength: 16000, latencyMs: 50 }, { id: "lm", kind: "provider", healthy: true, local: true, contextLength: 16000, latencyMs: 5 }] });
assert.equal(preferredRoute.selected.id, "remote");

const job = engine.n.centralJobs.enqueue({ id: "job-1", idempotencyKey: "idem-1", kind: "inference", payload: {}, createdAt: 1, requestId: "job-enqueue" });
assert.equal(engine.n.centralJobs.enqueue({ id: "job-duplicate", idempotencyKey: "idem-1", kind: "inference", payload: {}, createdAt: 2, requestId: "job-enqueue-duplicate" }).id, job.id);
engine.n.centralJobs.claim({ id: job.id, route: route.selected, ownerId: "node-check", expiresAt: 1000, requestId: "job-claim" });
engine.n.centralInference.begin({ jobId: job.id, modelId: "qwen", contextLength: 16000, requestId: "inference-begin" });
engine.n.centralInference.push({ jobId: job.id, sequence: 0, content: "hello", requestId: "inference-chunk" });
assert.throws(() => engine.n.centralInference.push({ jobId: job.id, sequence: 2, content: "bad", requestId: "inference-bad-sequence" }), /sequence/);

const pairing = engine.n.centralLanAccess.createPairing({ id: "pairing-1", codeHash: "hash", expiresAt: 1000, requestId: "pair-create" });
const session = engine.n.centralLanAccess.pair({ pairingId: pairing.id, codeHash: "hash", now: 1, sessionId: "session-1", sessionExpiresAt: 2000, requestId: "pair-use" });
assert.deepEqual(session.scopes, ["view", "inference.contribute"]);
assert.equal(engine.n.centralHarnesses.getSnapshot().selectedHarnessId, "conversation-harness");
const codingProfile = engine.n.centralHarnesses.getSnapshot().profiles.find((item) => item.id === "coding-harness");
const harnessAdapter = new ProcessHarnessAdapter();
const executorRegistry = new HarnessExecutorRegistry({ processHarness: harnessAdapter });
assert.deepEqual(executorRegistry.assertProfile(codingProfile, { sandbox: "workspace-write" }), { sandbox: "workspace-write", allowedTools: ["workspace.read", "workspace.write"] });
assert.throws(() => executorRegistry.assertProfile({ ...codingProfile, tools: ["network.unrestricted"] }, { sandbox: "workspace-write" }), /unsupported tool/);
assert.equal(executorRegistry.context({ runId: "run-check", scope: { id: "run:run-check" }, sandbox: { kind: "workspace-copy" }, toolPolicy: { allowedTools: ["workspace.read"] } }).runId, "run-check");
const codexArgs = harnessAdapter.buildCodexArgs(codingProfile, "act", { sandbox: "workspace-write", model: "qwen3.5-4b", oss: true, localProvider: "lmstudio" }, resolve("."), "proof prompt");
assert.equal(codexArgs.includes("--model") && codexArgs.includes("--oss") && codexArgs.includes("--local-provider"), true);
assert.equal(codexArgs.some((value) => value.includes("dangerously-bypass")), false);
assert.throws(() => harnessAdapter.buildCodexArgs(codingProfile, "act", { sandbox: "danger-full-access" }, resolve("."), "bad"), /not allowed/);
assert.throws(() => harnessAdapter.buildCodexArgs(codingProfile, "act", { sandbox: "workspace-write", localProvider: "lmstudio" }, resolve("."), "bad"), /requires --oss/);
const gatewayArgs = harnessAdapter.buildCodexArgs(codingProfile, "act", { sandbox: "workspace-write" }, resolve("."), "gateway proof", { gateway: { baseUrl: "http://127.0.0.1:47800/v1", token: "not-in-args" } });
assert.equal(gatewayArgs.includes("--local-provider") || gatewayArgs.includes("--oss"), false);
assert.throws(() => harnessAdapter.buildCodexArgs(codingProfile, "act", { sandbox: "workspace-write", oss: true, localProvider: "lmstudio" }, resolve("."), "bad", { gateway: { baseUrl: "http://127.0.0.1:47800/v1" } }), /cannot override/);

for (const [domainPath, apiName] of graph.apis.map((api) => [api.domainPath, api.apiName])) {
  if (!domainPath.startsWith("n:central")) continue;
  const api = engine.n[apiName];
  const snapshot = api.getSnapshot();
  api.reset();
  api.loadSnapshot(snapshot);
  assert.deepEqual(api.getSnapshot(), snapshot, `${domainPath} snapshot must round-trip`);
}

assert.throws(() => createEngine({ kits: [createCentralPolicyKit()] }), /requires missing token/);

const updateRegistry = JSON.parse(readFileSync(resolve("../updates/update-registry.json"), "utf8"));
assert.equal(updateRegistry.schemaVersion, 1);
assert.equal(typeof updateRegistry.recommended, "string");
assert.equal(Array.isArray(updateRegistry.versions), true);
const versionRefs = new Set(updateRegistry.versions.map((entry) => entry.ref));
assert.equal(versionRefs.size, updateRegistry.versions.length);
assert.equal(versionRefs.has(updateRegistry.recommended), true);
for (const ref of Object.values(updateRegistry.channels).filter(Boolean)) assert.equal(versionRefs.has(ref), true, `channel ref ${ref} must be registered`);
for (const entry of updateRegistry.versions) {
  assert.equal(entry.version, entry.ref);
  assert.match(entry.ref, /^(main|\d+\.\d+\.\d+)$/);
  assert.equal(["stable", "preview", "development", "retired"].includes(entry.status), true);
  assert.equal(["public", "internal"].includes(entry.availability), true);
  assert.equal(typeof entry.installable, "boolean");
  assert.equal(entry.runtime?.type, "esm");
  assert.equal(entry.runtime?.root, "NexusCentral-V2");
  assert.equal(Array.isArray(entry.platforms) && entry.platforms.length > 0, true);
  assert.match(entry.minimumBootstrap, /^\d+\.\d+\.\d+$/);
}
const recommendedVersion = updateRegistry.versions.find((entry) => entry.version === updateRegistry.recommended);
assert.equal(recommendedVersion?.status, "stable");
assert.equal(recommendedVersion?.availability, "public");
assert.equal(recommendedVersion?.installable, true);

const forbiddenImports = ["electron", "node:fs", "node:dgram", "node:http", "node:https", "node:child_process", "onnxruntime-node", "onnxruntime-web"];
function sources(directory) {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => entry.isDirectory() ? sources(join(directory, entry.name)) : entry.name.endsWith(".mjs") ? [join(directory, entry.name)] : []);
}
for (const sourcePath of sources(resolve("kits"))) {
  const source = readFileSync(sourcePath, "utf8");
  for (const forbidden of forbiddenImports) assert.equal(source.includes(forbidden), false, `${sourcePath} imports forbidden host module ${forbidden}`);
}

console.log(JSON.stringify({ status: "passed", graphPaths: expectedPaths, domainCount: expectedPaths.length, denyByDefault: true, writerLease: true, hostRouting: true, orderedStreams: true, safeCodexFlags: true, browserSessionScopes: true, snapshotRoundTrip: true, versionRegistry: true, forbiddenHostImports: false }, null, 2));
