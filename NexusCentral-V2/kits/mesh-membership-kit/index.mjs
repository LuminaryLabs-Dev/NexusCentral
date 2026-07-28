import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralMeshMembershipKit() {
  return createStateKit({
    id: "n-central-mesh-membership-kit", domain: "mesh-membership", domainPath: "n:central:mesh-membership", apiName: "centralMeshMembership",
    name: "NexusCentral Mesh Membership", summary: "Owns peer leases, topology, capabilities, and trust state.",
    requires: ["n:central:node", "n:central:audit"], outputs: ["mesh-membership.changed"], resetPolicy: "remove remote peers while preserving local identity",
    initialState: { networkId: null, networks: {}, peers: [] },
    transitions: {
      join(state, payload) {
        const transport = String(payload.transport || "lan");
        state.networkId = String(payload.networkId);
        state.networks = { ...state.networks, [transport]: { networkId: String(payload.networkId), connectedAt: Number(payload.connectedAt || Date.now()) } };
        return { state };
      },
      observePeer(state, payload) {
        const peer = { nodeId: String(payload.nodeId), transport: String(payload.transport || "lan"), transportPeerId: String(payload.transportPeerId || payload.nodeId), displayName: String(payload.displayName || payload.nodeId), addresses: payload.addresses || [], capabilities: payload.capabilities || [], trust: String(payload.trust || "member"), expiresAt: Number(payload.expiresAt) };
        state.peers = [...state.peers.filter((item) => !(item.nodeId === peer.nodeId && item.transport === peer.transport)), peer];
        return { state, result: peer };
      },
      leave(state, payload) {
        const transport = String(payload.transport || "lan");
        state.peers = state.peers.filter((item) => item.transport !== transport);
        const { [transport]: _removed, ...networks } = state.networks; state.networks = networks;
        state.networkId = Object.values(networks)[0]?.networkId || null;
        return { state };
      },
      expire(state, payload) {
        state.peers = state.peers.filter((item) => item.expiresAt > Number(payload.now));
        return { state };
      }
    },
    reset(state, empty) {
      return { ...empty, networkId: state.networkId, networks: state.networks };
    }
  });
}
