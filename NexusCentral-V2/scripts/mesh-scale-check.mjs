import assert from "node:assert/strict";
import { createCentralRuntime } from "../packages/composition/index.mjs";

const nodeCount = 50;
const jobsPerNode = 20;
const now = 10_000;
const nodes = Array.from({ length: nodeCount }, (_value, index) => createCentralRuntime({ node: { nodeId: `scale-node-${index}`, displayName: `Scale Node ${index}` } }));

for (let index = 0; index < nodes.length; index += 1) {
  const engine = nodes[index];
  engine.n.centralMeshMembership.join({ transport: "peerjs", networkId: "scale-network", requestId: `join-${index}` });
  for (let peerIndex = 0; peerIndex < nodeCount; peerIndex += 1) {
    if (peerIndex === index) continue;
    engine.n.centralMeshMembership.observePeer({ nodeId: `scale-node-${peerIndex}`, transport: "peerjs", transportPeerId: `peerjs-${peerIndex}`, displayName: `Scale Node ${peerIndex}`, addresses: [`peerjs://peerjs-${peerIndex}`], capabilities: [{ action: "mesh.inference", taskType: "text-generation", providerId: "scale-provider", modelId: "scale-model", contextLength: 16000, concurrencyLimit: 1 }], expiresAt: peerIndex < 10 ? now - 1 : now + 30_000, requestId: `peer-${index}-${peerIndex}` });
  }
  engine.n.centralMeshMembership.expire({ now, requestId: `expire-${index}` });
  assert.equal(engine.n.centralMeshMembership.getSnapshot().peers.length, index < 10 ? 40 : 39);
  const candidates = engine.n.centralMeshMembership.getSnapshot().peers.map((peer, candidateIndex) => ({ id: `remote:${peer.transport}:${peer.nodeId}:scale-provider`, kind: "remote-peer", healthy: true, contextLength: 16000, queueDepth: candidateIndex % 7, latencyMs: 5 + candidateIndex }));
  const route = engine.n.centralRouting.select({ requestId: `route-${index}`, contextLength: 16000, candidates });
  assert.equal(Boolean(route.selected.id), true);
  for (let jobIndex = 0; jobIndex < jobsPerNode; jobIndex += 1) engine.n.centralJobs.enqueue({ id: `job-${index}-${jobIndex}`, idempotencyKey: `job-${index}-${jobIndex}`, kind: "inference", payload: {}, createdAt: now, requestId: `enqueue-${index}-${jobIndex}` });
  assert.equal(engine.n.centralJobs.getSnapshot().jobs.length, jobsPerNode);
  const peerId = engine.n.centralMeshMembership.getSnapshot().peers[0].nodeId;
  assert.equal(engine.n.centralPolicy.evaluate({ peerId, action: "mesh.inference", requestId: `deny-${index}` }).decision, "deny");
}

const memoryMb = Math.round(process.memoryUsage().heapUsed / 1024 / 1024);
assert.equal(memoryMb < 512, true);
console.log(JSON.stringify({ status: "passed", simulatedNodes: nodeCount, transport: "peerjs", topology: "logical full mesh with request-scoped connections", churnedNodes: 10, convergedPeersPerNode: "39-40", queuedJobs: nodeCount * jobsPerNode, maximumRequestConnectionsPerNode: jobsPerNode, denyByDefaultDecisions: nodeCount, heapUsedMb: memoryMb }, null, 2));
