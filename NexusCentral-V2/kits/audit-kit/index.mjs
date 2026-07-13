import { defineDomainServiceKit, defineResource } from "nexusengine";

const AuditState = defineResource("nexuscentral.v2.auditState");

function initialState() {
  return { entries: [], cursor: 0, segment: 1, processedRequestIds: [] };
}

export function createCentralAuditKit() {
  return defineDomainServiceKit({
    id: "n-central-audit-kit",
    domain: "central-audit",
    domainPath: "n:central:audit",
    parentDomainPath: "n:central",
    apiName: "centralAudit",
    stability: "experimental",
    version: "0.1.0",
    services: ["append", "query", "snapshot", "rotate"],
    requires: ["n:central:node"],
    resources: { AuditState },
    inputs: ["audit.append", "audit.rotate"],
    outputs: ["audit.receipt", "audit.snapshot"],
    metadata: {
      name: "NexusCentral Audit",
      summary: "Owns append-only operation and policy receipts.",
      resetPolicy: "rotate active segment; destructive clear is test-only",
      snapshotPolicy: "cursor-addressed public receipts"
    },
    initWorld({ world }) {
      world.setResource(AuditState, initialState());
    },
    createApi({ world }) {
      return Object.freeze({
        append(entry, requestId) {
          const state = world.getResource(AuditState);
          if (requestId && state.processedRequestIds.includes(requestId)) {
            return structuredClone(state.entries.find((item) => item.requestId === requestId));
          }
          const receipt = {
            cursor: state.cursor + 1,
            segment: state.segment,
            requestId: requestId || `audit-${state.cursor + 1}`,
            ...structuredClone(entry)
          };
          world.setResource(AuditState, {
            ...state,
            entries: [...state.entries, receipt],
            cursor: receipt.cursor,
            processedRequestIds: [...state.processedRequestIds, receipt.requestId]
          });
          return structuredClone(receipt);
        },
        getSnapshot() {
          return structuredClone(world.getResource(AuditState));
        },
        loadSnapshot(snapshot) {
          const next = { ...initialState(), ...structuredClone(snapshot) };
          world.setResource(AuditState, next);
          return structuredClone(next);
        },
        reset() {
          const state = world.getResource(AuditState);
          const next = { entries: [], cursor: 0, segment: state.segment + 1, processedRequestIds: [] };
          world.setResource(AuditState, next);
          return structuredClone(next);
        }
      });
    }
  });
}
