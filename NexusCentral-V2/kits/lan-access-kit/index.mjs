import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralLanAccessKit() {
  return createStateKit({
    id: "n-central-lan-access-kit", domain: "lan-access", domainPath: "n:central:lan-access", apiName: "centralLanAccess",
    name: "NexusCentral LAN Access", summary: "Owns pairing grants, browser sessions, scopes, and revocation.",
    requires: ["n:central:node", "n:central:policy", "n:central:audit"], outputs: ["lan-access.changed"], resetPolicy: "revoke browser sessions and pairing grants",
    initialState: { pairings: [], sessions: [] },
    transitions: {
      createPairing(state, payload) {
        const pairing = { id: String(payload.id), codeHash: String(payload.codeHash), expiresAt: Number(payload.expiresAt), used: false };
        state.pairings.push(pairing);
        return { state, result: pairing };
      },
      pair(state, payload) {
        const pairing = state.pairings.find((item) => item.id === payload.pairingId && !item.used && item.expiresAt > Number(payload.now));
        if (!pairing || pairing.codeHash !== payload.codeHash) throw new Error("Pairing code is invalid or expired.");
        pairing.used = true;
        const session = { id: String(payload.sessionId), clientName: String(payload.clientName || "LAN browser"), scopes: ["view", "inference.contribute"], expiresAt: Number(payload.sessionExpiresAt), revoked: false };
        state.sessions.push(session);
        return { state, result: session };
      },
      grantScope(state, payload) {
        const session = state.sessions.find((item) => item.id === payload.sessionId && !item.revoked);
        if (!session) throw new Error("Unknown session.");
        if (!session.scopes.includes(payload.scope)) session.scopes.push(String(payload.scope));
        return { state, result: session };
      },
      revoke(state, payload) {
        const session = state.sessions.find((item) => item.id === payload.sessionId);
        if (session) session.revoked = true;
        return { state, result: session || null };
      },
      expire(state, payload) {
        const now = Number(payload.now);
        state.pairings = state.pairings.filter((item) => !item.used && item.expiresAt > now);
        state.sessions = state.sessions.filter((item) => !item.revoked && item.expiresAt > now);
        return { state };
      }
    }
  });
}
