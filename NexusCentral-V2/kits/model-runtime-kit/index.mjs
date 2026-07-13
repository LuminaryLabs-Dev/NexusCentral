import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralModelRuntimeKit() {
  return createStateKit({
    id: "n-central-model-runtime-kit", domain: "model-runtime", domainPath: "n:central:model-runtime", apiName: "centralModelRuntime",
    name: "NexusCentral Model Runtime", summary: "Owns loaded and ephemeral model runtime leases.",
    requires: ["n:central:model-catalog", "n:central:node", "n:central:audit"], outputs: ["model-runtime.changed"], resetPolicy: "expire all runtime leases",
    initialState: { runtimes: [] },
    transitions: {
      register(state, payload) {
        const runtime = { id: String(payload.id), kind: String(payload.kind), modelId: String(payload.modelId), taskType: String(payload.taskType || "text-generation"), sessionId: String(payload.sessionId || ""), fingerprint: String(payload.fingerprint || ""), executionProvider: String(payload.executionProvider || "unknown"), contextLength: Number(payload.contextLength || 16000), concurrency: Number(payload.concurrency || 1), status: String(payload.status || "ready"), expiresAt: Number(payload.expiresAt || 0) };
        state.runtimes = [...state.runtimes.filter((item) => item.id !== runtime.id), runtime];
        return { state, result: runtime };
      },
      remove(state, payload) {
        state.runtimes = state.runtimes.filter((item) => item.id !== payload.id && item.sessionId !== payload.sessionId);
        return { state };
      },
      expire(state, payload) {
        state.runtimes = state.runtimes.filter((item) => !item.expiresAt || item.expiresAt > Number(payload.now));
        return { state };
      }
    }
  });
}
