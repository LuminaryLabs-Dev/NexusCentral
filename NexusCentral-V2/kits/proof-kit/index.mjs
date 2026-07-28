import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralProofKit() {
  return createStateKit({
    id: "n-central-proof-kit", domain: "proof", domainPath: "n:central:proof", apiName: "centralProof",
    name: "NexusCentral Proof", summary: "Owns evidence references, assertions, and promotion decisions.",
    requires: ["n:central:jobs", "n:central:artifacts", "n:central:audit"], outputs: ["proof.recorded"], resetPolicy: "preserve completed evidence and clear transient review state",
    initialState: { records: [] },
    transitions: {
      record(state, payload) {
        const record = { id: String(payload.id), jobId: String(payload.jobId), assertions: payload.assertions || [], artifacts: payload.artifacts || [], status: String(payload.status), createdAt: Number(payload.createdAt) };
        state.records = [...state.records.filter((item) => item.id !== record.id), record];
        return { state, result: record };
      }
    },
    reset(state, empty) {
      return { ...empty, records: state.records };
    }
  });
}
