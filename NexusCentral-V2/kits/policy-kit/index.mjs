import { defineDomainServiceKit, defineResource } from "nexusengine";

const PolicyState = defineResource("nexuscentral.v2.policyState");

function initialState() {
  return {
    defaultDecision: "deny",
    grants: [],
    decisions: [],
    revision: 0,
    processedRequestIds: []
  };
}

export function createCentralPolicyKit() {
  return defineDomainServiceKit({
    id: "n-central-policy-kit",
    domain: "central-policy",
    domainPath: "n:central:policy",
    parentDomainPath: "n:central",
    apiName: "centralPolicy",
    stability: "experimental",
    version: "0.1.0",
    services: ["evaluate", "grant", "revoke", "snapshot", "reset"],
    requires: ["n:central:node", "n:central:audit"],
    resources: { PolicyState },
    inputs: ["policy.evaluate", "policy.grant", "policy.revoke", "policy.reset"],
    outputs: ["policy.decision", "policy.grant-changed"],
    metadata: {
      name: "NexusCentral Policy",
      summary: "Owns local peer grants and deny-by-default admission decisions.",
      resetPolicy: "remove grants and return to deny-by-default",
      snapshotPolicy: "serializable grants and decisions without secrets"
    },
    initWorld({ world }) {
      world.setResource(PolicyState, initialState());
    },
    createApi({ world, engine }) {
      return Object.freeze({
        grant({ peerId, action, grantId }) {
          const state = world.getResource(PolicyState);
          if (grantId && state.processedRequestIds.includes(grantId)) return structuredClone(state);
          const grant = { peerId: String(peerId), action: String(action), grantId: grantId || `grant-${state.revision + 1}` };
          const grants = [...state.grants.filter((item) => item.grantId !== grant.grantId), grant];
          const next = {
            ...state,
            grants,
            revision: state.revision + 1,
            processedRequestIds: [...state.processedRequestIds, grant.grantId]
          };
          world.setResource(PolicyState, next);
          engine.n.centralAudit.append({ type: "policy.grant", peerId: grant.peerId, action: grant.action }, `audit:${grant.grantId}`);
          return structuredClone(next);
        },
        revoke({ grantId, requestId }) {
          const state = world.getResource(PolicyState);
          if (requestId && state.processedRequestIds.includes(requestId)) return structuredClone(state);
          const next = {
            ...state,
            grants: state.grants.filter((item) => item.grantId !== grantId),
            revision: state.revision + 1,
            processedRequestIds: requestId ? [...state.processedRequestIds, requestId] : state.processedRequestIds
          };
          world.setResource(PolicyState, next);
          engine.n.centralAudit.append({ type: "policy.revoke", grantId: String(grantId) }, `audit:${requestId || grantId}`);
          return structuredClone(next);
        },
        evaluate({ peerId, action, requestId }) {
          const state = world.getResource(PolicyState);
          const existing = state.decisions.find((item) => item.requestId === requestId);
          if (existing) return structuredClone(existing);
          const allowed = state.grants.some((grant) => grant.peerId === peerId && grant.action === action);
          const decision = { requestId, peerId, action, decision: allowed ? "allow" : state.defaultDecision };
          world.setResource(PolicyState, {
            ...state,
            decisions: [...state.decisions, decision],
            revision: state.revision + 1,
            processedRequestIds: [...state.processedRequestIds, requestId]
          });
          engine.n.centralAudit.append({ type: "policy.decision", ...decision }, `audit:${requestId}`);
          return structuredClone(decision);
        },
        getSnapshot() {
          return structuredClone(world.getResource(PolicyState));
        },
        loadSnapshot(snapshot) {
          const next = { ...initialState(), ...structuredClone(snapshot) };
          world.setResource(PolicyState, next);
          return structuredClone(next);
        },
        reset() {
          const next = initialState();
          world.setResource(PolicyState, next);
          engine.n.centralAudit.append({ type: "policy.reset", decision: "deny" });
          return structuredClone(next);
        }
      });
    }
  });
}
