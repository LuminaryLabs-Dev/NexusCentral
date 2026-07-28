import { DEFAULT_CONTEXT_LENGTH } from "@nexuscentral/contracts";

export class ModelLifecycleAdapter {
  constructor({ fetchImpl = fetch }) { this.fetchImpl = fetchImpl; }

  connection(profile) {
    if (!profile || profile.kind !== "lm-studio") throw new Error("Configure an LM Studio provider first.");
    return profile.baseUrl.replace(/\/$/, "").replace(/\/v1$/, "");
  }

  async request(profile, path, options = {}) {
    const response = await this.fetchImpl(`${this.connection(profile)}${path}`, { ...options, headers: { Accept: "application/json", ...(options.body ? { "Content-Type": "application/json" } : {}), ...(options.headers || {}) }, signal: AbortSignal.timeout(options.timeoutMs || 15000) });
    if (!response.ok) throw new Error(`LM Studio returned HTTP ${response.status}.`);
    return response.json();
  }

  async listInstalled(profile) {
    const body = await this.request(profile, "/api/v1/models");
    return (body.models || body.data || []).map((item) => ({ id: String(item.id || item.model), name: String(item.display_name || item.name || item.id || item.model), type: String(item.type || "llm"), status: String(item.state || item.status || "downloaded"), instanceId: String(item.instance_id || "") })).filter((item) => item.id);
  }

  async search(query) {
    const params = new URLSearchParams({ search: String(query), filter: "gguf", sort: "downloads", direction: "-1", limit: "12" });
    const response = await this.fetchImpl(`https://huggingface.co/api/models?${params}`, { headers: { Accept: "application/json" }, signal: AbortSignal.timeout(10000) });
    if (!response.ok) throw new Error(`Hugging Face returned HTTP ${response.status}.`);
    return (await response.json()).map((item) => ({ id: String(item.modelId || item.id), name: String(item.modelId || item.id), downloads: Number(item.downloads || 0), likes: Number(item.likes || 0) })).filter((item) => item.id);
  }

  download(profile, modelId) {
    return this.request(profile, "/api/v1/models/download", { method: "POST", body: JSON.stringify({ model: `https://huggingface.co/${modelId}` }), timeoutMs: 30000 });
  }

  load(profile, modelId) {
    return this.request(profile, "/api/v1/models/load", { method: "POST", body: JSON.stringify({ model: modelId, context_length: DEFAULT_CONTEXT_LENGTH }), timeoutMs: 5 * 60_000 });
  }

  unload(profile, instanceId) {
    return this.request(profile, "/api/v1/models/unload", { method: "POST", body: JSON.stringify({ instance_id: instanceId }) });
  }
}
