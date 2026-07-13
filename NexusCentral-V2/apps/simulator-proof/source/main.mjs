import { createCentralSimulation, getCentralGraphSnapshot } from "./packages/composition/index.mjs";

const steps = document.getElementById("proof-steps");
const status = document.getElementById("proof-status");
const pathCount = document.getElementById("path-count");

function addStep(label, value, passed) {
  const item = document.createElement("li");
  item.innerHTML = `<span>${label}</span><strong>${value}</strong>`;
  item.dataset.passed = String(passed);
  steps.appendChild(item);
}

function runProof() {
  steps.innerHTML = "";
  const engine = createCentralSimulation();
  const graph = getCentralGraphSnapshot(engine);
  pathCount.textContent = String(graph.paths.length);

  const denied = engine.n.centralPolicy.evaluate({
    peerId: "peer-beta",
    action: "mesh.ping",
    requestId: "browser-denied"
  });
  addStep("Unknown peer requests mesh.ping", denied.decision.toUpperCase(), denied.decision === "deny");

  engine.n.centralPolicy.grant({
    peerId: "peer-beta",
    action: "mesh.ping",
    grantId: "browser-grant"
  });
  const allowed = engine.n.centralPolicy.evaluate({
    peerId: "peer-beta",
    action: "mesh.ping",
    requestId: "browser-allowed"
  });
  addStep("Local node grants exact action", allowed.decision.toUpperCase(), allowed.decision === "allow");

  const snapshot = engine.n.centralPolicy.getSnapshot();
  engine.n.centralPolicy.reset();
  const resetDecision = engine.n.centralPolicy.evaluate({
    peerId: "peer-beta",
    action: "mesh.ping",
    requestId: "browser-reset"
  });
  addStep("Policy resets", resetDecision.decision.toUpperCase(), resetDecision.decision === "deny");

  engine.n.centralPolicy.loadSnapshot(snapshot);
  const restored = engine.n.centralPolicy.evaluate({
    peerId: "peer-beta",
    action: "mesh.ping",
    requestId: "browser-restored"
  });
  addStep("Snapshot restores explicit grant", restored.decision.toUpperCase(), restored.decision === "allow");

  engine.n.centralWorkspace.select({ workspaceId: "sandbox", name: "Sandbox", revision: 1, requestId: "browser-workspace" });
  engine.n.centralWorkspace.acquireWriter({ jobId: "job-a", holderId: "local", expectedRevision: 1, expiresAt: 1000, requestId: "browser-writer" });
  let writerConflict = false;
  try { engine.n.centralWorkspace.acquireWriter({ jobId: "job-b", holderId: "peer", expectedRevision: 1, expiresAt: 1000, requestId: "browser-conflict" }); }
  catch (_error) { writerConflict = true; }
  addStep("Second workspace writer", writerConflict ? "BLOCKED" : "ALLOWED", writerConflict);

  const route = engine.n.centralRouting.select({ requestId: "browser-route", contextLength: 16000, candidates: [
    { id: "cloud", kind: "provider", healthy: true, contextLength: 16000, latencyMs: 50 },
    { id: "host-lm-studio", kind: "provider", healthy: true, local: true, contextLength: 16000, latencyMs: 5 }
  ] });
  addStep("Host selects inference route", route.selected.id, route.selected.id === "host-lm-studio");

  engine.n.centralLanAccess.createPairing({ id: "pair", codeHash: "hash", expiresAt: 1000, requestId: "browser-pairing" });
  const session = engine.n.centralLanAccess.pair({ pairingId: "pair", codeHash: "hash", now: 1, sessionId: "browser-session", sessionExpiresAt: 2000, requestId: "browser-session" });
  const scoped = session.scopes.join(" + ");
  addStep("New LAN browser scope", scoped, scoped === "view + inference.contribute");

  const proof = {
    ready: true,
    graphPaths: graph.paths.map((entry) => entry.path),
    denyByDefault: denied.decision === "deny",
    explicitGrant: allowed.decision === "allow",
    denyAfterReset: resetDecision.decision === "deny",
    allowAfterRestore: restored.decision === "allow",
    writerConflictBlocked: writerConflict,
    hostRouteSelected: route.selected.id === "host-lm-studio",
    browserScopeLimited: scoped === "view + inference.contribute",
    auditReceipts: engine.n.centralAudit.getSnapshot().entries.length
  };
  window.__NEXUS_TEST_STATE__ = proof;
  status.textContent = Object.values(proof).includes(false) ? "Proof failed" : "Proof passed";
  status.dataset.passed = String(status.textContent === "Proof passed");
}

document.getElementById("replay-proof").addEventListener("click", runProof);
runProof();
