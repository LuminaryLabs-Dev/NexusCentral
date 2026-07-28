import { defineDomainServiceKit, defineResource } from "nexusengine";

const CentralState = defineResource("nexuscentral.v2.centralState");

function initialState() {
  return {
    graphVersion: "0.1.0",
    mode: "simulation",
    readiness: "initializing",
    revision: 0,
    processedRequestIds: []
  };
}

export function createCentralKit() {
  return defineDomainServiceKit({
    id: "n-central-kit",
    domain: "central",
    domainPath: "n:central",
    apiName: "central",
    stability: "experimental",
    version: "0.1.0",
    services: ["readiness", "snapshot", "reset"],
    requires: ["n:realtime"],
    resources: { CentralState },
    inputs: ["central.set-readiness", "central.load-snapshot", "central.reset"],
    outputs: ["central.readiness-changed", "central.snapshot"],
    metadata: {
      name: "NexusCentral Foundation",
      summary: "Owns product graph version, runtime mode, and aggregate readiness.",
      resetPolicy: "clear transient readiness while preserving graph identity",
      snapshotPolicy: "serializable public product state"
    },
    initWorld({ world }) {
      world.setResource(CentralState, initialState());
    },
    createApi({ world }) {
      return Object.freeze({
        setReadiness(readiness, requestId) {
          const state = world.getResource(CentralState);
          if (requestId && state.processedRequestIds.includes(requestId)) return structuredClone(state);
          const next = {
            ...state,
            readiness: String(readiness || "unknown"),
            revision: state.revision + 1,
            processedRequestIds: requestId ? [...state.processedRequestIds, requestId] : state.processedRequestIds
          };
          world.setResource(CentralState, next);
          return structuredClone(next);
        },
        getSnapshot() {
          return structuredClone(world.getResource(CentralState));
        },
        loadSnapshot(snapshot) {
          const next = { ...initialState(), ...structuredClone(snapshot) };
          world.setResource(CentralState, next);
          return structuredClone(next);
        },
        reset() {
          const current = world.getResource(CentralState);
          const next = { ...initialState(), graphVersion: current.graphVersion, mode: current.mode };
          world.setResource(CentralState, next);
          return structuredClone(next);
        }
      });
    }
  });
}
