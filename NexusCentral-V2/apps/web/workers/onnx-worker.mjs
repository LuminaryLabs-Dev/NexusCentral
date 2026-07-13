import * as ort from "onnxruntime-web/webgpu";

let session = null;
let manifest = null;

function tensor(spec) {
  const constructors = { float32: Float32Array, int32: Int32Array, int64: BigInt64Array, bool: Uint8Array };
  const Type = constructors[spec.type];
  if (!Type) throw new Error(`Unsupported smoke tensor type: ${spec.type}`);
  const values = spec.type === "int64" ? spec.data.map(BigInt) : spec.data;
  return new ort.Tensor(spec.type, new Type(values), spec.dims);
}

self.onmessage = async ({ data }) => {
  try {
    if (data.type === "load") {
      manifest = data.manifest;
      if (!manifest?.id || !manifest.smokeInputs) throw new Error("Model package manifest requires id and smokeInputs.");
      const fingerprint = [...new Uint8Array(await crypto.subtle.digest("SHA-256", data.bytes))].map((value) => value.toString(16).padStart(2, "0")).join("");
      const executionProvider = "gpu" in navigator ? "webgpu" : "wasm";
      session = await ort.InferenceSession.create(data.bytes, { executionProviders: [executionProvider] });
      const feeds = Object.fromEntries(Object.entries(manifest.smokeInputs).map(([name, spec]) => [name, tensor(spec)]));
      await session.run(feeds);
      self.postMessage({ type: "loaded", fingerprint, executionProvider });
    }
    if (data.type === "run") {
      if (!session) throw new Error("No browser model is loaded.");
      if (!data.feeds) throw new Error("This ONNX package accepts normalized tensor feeds only; a text generation adapter is required for chat.");
      const outputs = await session.run(Object.fromEntries(Object.entries(data.feeds).map(([name, spec]) => [name, tensor(spec)])));
      self.postMessage({ type: "result", jobId: data.jobId, outputs: Object.fromEntries(Object.entries(outputs).map(([name, value]) => [name, { type: value.type, dims: value.dims, data: Array.from(value.data, String) }])) });
    }
  } catch (error) { self.postMessage({ type: "error", error: error.message, jobId: data.jobId }); }
};
