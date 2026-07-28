import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralArtifactsKit() {
  return createStateKit({
    id: "n-central-artifacts-kit", domain: "artifacts", domainPath: "n:central:artifacts", apiName: "centralArtifacts",
    name: "NexusCentral Artifacts", summary: "Owns content-addressed artifact metadata and provenance.",
    requires: ["n:central:workspace", "n:central:audit"], outputs: ["artifacts.recorded"], resetPolicy: "clear indexes without deleting bytes",
    initialState: { artifacts: [] },
    transitions: {
      record(state, payload) {
        const artifact = {
          id: String(payload.id), hash: String(payload.hash), kind: String(payload.kind), jobId: String(payload.jobId || payload.runId || ""),
          runId: String(payload.runId || payload.jobId || ""), path: String(payload.path || ""), bytes: Number(payload.bytes || 0),
          mediaType: String(payload.mediaType || "application/octet-stream"), stage: String(payload.stage || ""), provenance: payload.provenance || {}, createdAt: Number(payload.createdAt)
        };
        state.artifacts = [...state.artifacts.filter((item) => item.id !== artifact.id), artifact];
        return { state, result: artifact };
      }
    }
  });
}
