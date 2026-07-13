import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralInferenceKit() {
  return createStateKit({
    id: "n-central-inference-kit", domain: "inference", domainPath: "n:central:inference", apiName: "centralInference",
    name: "NexusCentral Inference", summary: "Owns normalized inference requests and ordered stream state.",
    requires: ["n:central:jobs", "n:central:model-runtime"], outputs: ["inference.chunk", "inference.finished"], resetPolicy: "clear transient streams",
    initialState: { streams: [] },
    transitions: {
      begin(state, payload) {
        const stream = { jobId: String(payload.jobId), modelId: String(payload.modelId || ""), contextLength: Number(payload.contextLength || 16000), status: "streaming", chunks: [], usage: null };
        state.streams = [...state.streams.filter((item) => item.jobId !== stream.jobId), stream];
        return { state, result: stream };
      },
      push(state, payload) {
        const stream = state.streams.find((item) => item.jobId === payload.jobId);
        if (!stream || stream.status !== "streaming") throw new Error("Inference stream is not active.");
        const expected = stream.chunks.length;
        if (Number(payload.sequence) !== expected) throw new Error("Inference chunk sequence is invalid.");
        stream.chunks.push({ sequence: expected, content: String(payload.content || "") });
        return { state, result: stream.chunks.at(-1) };
      },
      finish(state, payload) {
        const stream = state.streams.find((item) => item.jobId === payload.jobId);
        if (!stream) throw new Error("Unknown inference stream.");
        stream.status = String(payload.status || "completed"); stream.usage = payload.usage || null;
        return { state, result: stream };
      }
    }
  });
}
