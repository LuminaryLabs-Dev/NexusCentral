import { defineDomainServiceKit, defineResource } from "nexusengine";

const NodeState = defineResource("nexuscentral.v2.nodeState");

function initialState(config = {}) {
  return {
    nodeId: config.nodeId || "sim-node-alpha",
    displayName: config.displayName || "Simulator Node",
    status: "online",
    capabilities: [],
    revision: 0,
    processedRequestIds: []
  };
}

export function createCentralNodeKit(config = {}) {
  return defineDomainServiceKit({
    id: "n-central-node-kit",
    domain: "central-node",
    domainPath: "n:central:node",
    parentDomainPath: "n:central",
    apiName: "centralNode",
    stability: "experimental",
    version: "0.1.0",
    services: ["identity", "profile", "capabilities", "snapshot", "reset"],
    requires: ["n:central"],
    resources: { NodeState },
    inputs: ["node.set-identity", "node.advertise-capability", "node.rename", "node.reset"],
    outputs: ["node.profile-changed", "node.capabilities-changed"],
    metadata: {
      name: "NexusCentral Node",
      summary: "Owns the public node profile and advertised local capabilities.",
      resetPolicy: "soft reset preserves node identity",
      snapshotPolicy: "public identity only; private keys forbidden"
    },
    initWorld({ world }) {
      world.setResource(NodeState, initialState(config));
    },
    createApi({ world }) {
      return Object.freeze({
        setIdentity(nodeId, requestId) {
          const state = world.getResource(NodeState);
          if (requestId && state.processedRequestIds.includes(requestId)) return structuredClone(state);
          const next = { ...state, nodeId: String(nodeId), revision: state.revision + 1, processedRequestIds: requestId ? [...state.processedRequestIds, requestId] : state.processedRequestIds };
          world.setResource(NodeState, next);
          return structuredClone(next);
        },
        rename(displayName, requestId) {
          const state = world.getResource(NodeState);
          if (requestId && state.processedRequestIds.includes(requestId)) return structuredClone(state);
          const next = { ...state, displayName: String(displayName), revision: state.revision + 1, processedRequestIds: requestId ? [...state.processedRequestIds, requestId] : state.processedRequestIds };
          world.setResource(NodeState, next);
          return structuredClone(next);
        },
        advertiseCapability(capability, requestId) {
          const state = world.getResource(NodeState);
          if (requestId && state.processedRequestIds.includes(requestId)) return structuredClone(state);
          const capabilities = Array.from(new Set([...state.capabilities, String(capability)])).sort();
          const next = {
            ...state,
            capabilities,
            revision: state.revision + 1,
            processedRequestIds: requestId ? [...state.processedRequestIds, requestId] : state.processedRequestIds
          };
          world.setResource(NodeState, next);
          return structuredClone(next);
        },
        getSnapshot() {
          return structuredClone(world.getResource(NodeState));
        },
        loadSnapshot(snapshot) {
          const next = { ...initialState(config), ...structuredClone(snapshot) };
          world.setResource(NodeState, next);
          return structuredClone(next);
        },
        reset() {
          const current = world.getResource(NodeState);
          const next = initialState({ nodeId: current.nodeId, displayName: current.displayName });
          world.setResource(NodeState, next);
          return structuredClone(next);
        }
      });
    }
  });
}
