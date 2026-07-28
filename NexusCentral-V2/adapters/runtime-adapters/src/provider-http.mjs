import { DEFAULT_CONTEXT_LENGTH, PROVIDER_TEMPLATES } from "@nexuscentral/contracts";

export class ProviderAdapterRegistry {
  constructor({ secretStore, fetchImpl = fetch }) {
    this.secretStore = secretStore;
    this.fetchImpl = fetchImpl;
    this.adapters = new Map([
      ["openai-chat", new OpenAiCompatibleAdapter(this)],
      ["anthropic-messages", new AnthropicMessagesAdapter(this)],
      ["google-generate", new GoogleGenerateAdapter(this)],
      ["azure-openai", new AzureOpenAiAdapter(this)],
      ["aws-bedrock", new AwsBedrockAdapter(this)]
    ]);
  }

  adapter(profile) {
    const protocol = profile.protocol || PROVIDER_TEMPLATES[profile.kind]?.protocol || "openai-chat";
    const adapter = this.adapters.get(protocol);
    if (!adapter) throw new Error(`Unsupported provider protocol: ${protocol}.`);
    return adapter;
  }

  secret(profile) { return profile.secretRef ? this.secretStore.get(profile.secretRef) : ""; }
  probe(profile) { return this.adapter(profile).probe(profile); }
  streamChat(profile, request, onChunk, signal) { return this.adapter(profile).stream(profile, request, onChunk, signal); }
  openAiChat(profile, request, signal) {
    const adapter = this.adapter(profile);
    if (typeof adapter.rawChat !== "function") throw new Error("Selected provider does not support the Codex OpenAI-compatible gateway.");
    return adapter.rawChat(profile, request, signal);
  }
  cancel(profile, requestId) { return this.adapter(profile).cancel?.(profile, requestId) || false; }
}

export class ProviderHttpAdapter extends ProviderAdapterRegistry {}

class BaseHttpAdapter {
  constructor(registry) { this.registry = registry; }
  template(profile) { return PROVIDER_TEMPLATES[profile.kind] || PROVIDER_TEMPLATES.custom; }
  base(profile) { return String(profile.baseUrl || this.template(profile).baseUrl).replace(/\/$/, ""); }
  secret(profile) {
    const value = this.registry.secret(profile);
    if (this.template(profile).apiKeyRequired && !value) throw new Error(`${this.template(profile).name} requires a credential reference.`);
    return value;
  }
  outputLimit(request) { return Math.max(1, Math.min(DEFAULT_CONTEXT_LENGTH, Number(request.maxOutputTokens || 1024))); }
}

class OpenAiCompatibleAdapter extends BaseHttpAdapter {
  headers(profile, json = false) {
    const headers = { Accept: "application/json" };
    if (json) headers["Content-Type"] = "application/json";
    const secret = this.secret(profile);
    if (secret) headers.Authorization = `Bearer ${secret}`;
    return headers;
  }

  async probe(profile) {
    const template = this.template(profile);
    if (template.healthPath || profile.healthPath) {
      const health = await this.registry.fetchImpl(`${this.base(profile)}${profile.healthPath || template.healthPath}`, { headers: this.headers(profile), signal: AbortSignal.timeout(8000) });
      if (!health.ok) throw new Error(`Provider readiness returned HTTP ${health.status}.`);
    }
    const path = profile.modelsPath || template.modelsPath;
    const response = await this.registry.fetchImpl(`${this.base(profile)}${path}`, { headers: this.headers(profile), signal: AbortSignal.timeout(8000) });
    if (!response.ok) throw new Error(`Provider returned HTTP ${response.status}.`);
    const body = await response.json();
    return normalizeModels(body.data || body.models || []);
  }

  async stream(profile, request, onChunk, signal) {
    const path = profile.inferencePath || this.template(profile).inferencePath;
    const body = { model: request.modelId || profile.modelId, messages: request.messages, stream: true, max_tokens: this.outputLimit(request) };
    if (profile.kind === "lm-studio") body.context_length = Number(request.contextLength || profile.contextLength || DEFAULT_CONTEXT_LENGTH);
    const response = await this.registry.fetchImpl(`${this.base(profile)}${path}`, { method: "POST", headers: this.headers(profile, true), signal, body: JSON.stringify(body) });
    if (!response.ok) throw new Error(`Provider returned HTTP ${response.status}.`);
    let visibleChunks = 0; let reasoningChunks = 0;
    await readSse(response, async (data) => {
      const content = data.choices?.[0]?.delta?.content || "";
      const reasoning = data.choices?.[0]?.delta?.reasoning_content || data.choices?.[0]?.delta?.reasoning || "";
      if (reasoning) reasoningChunks += 1;
      if (content) { visibleChunks += 1; await onChunk(content); }
    });
    if (!visibleChunks) throw new Error(reasoningChunks ? "Provider exhausted its response in reasoning tokens without final content." : "Provider completed without visible content.");
    return { visibleChunks, reasoningChunks };
  }

  async rawChat(profile, request, signal) {
    let path = profile.inferencePath || this.template(profile).inferencePath;
    if (profile.kind === "azure-openai" && !path.includes("api-version=")) path += `${path.includes("?") ? "&" : "?"}api-version=2024-10-21`;
    const body = { ...request, model: profile.modelId || request.model };
    const response = await this.registry.fetchImpl(`${this.base(profile)}${path}`, { method: "POST", headers: this.headers(profile, true), signal, body: JSON.stringify(body) });
    if (!response.ok) throw new Error(`Provider returned HTTP ${response.status}: ${(await response.text()).slice(0, 500)}`);
    return response;
  }
}

class AnthropicMessagesAdapter extends BaseHttpAdapter {
  headers(profile) { return { Accept: "application/json", "Content-Type": "application/json", "x-api-key": this.secret(profile), "anthropic-version": "2023-06-01" }; }
  async probe(profile) {
    const response = await this.registry.fetchImpl(`${this.base(profile)}/models`, { headers: this.headers(profile), signal: AbortSignal.timeout(8000) });
    if (!response.ok) throw new Error(`Anthropic returned HTTP ${response.status}.`);
    return normalizeModels((await response.json()).data || []);
  }
  async stream(profile, request, onChunk, signal) {
    const response = await this.registry.fetchImpl(`${this.base(profile)}/messages`, { method: "POST", headers: this.headers(profile), signal, body: JSON.stringify({ model: request.modelId || profile.modelId, messages: request.messages, max_tokens: this.outputLimit(request), stream: true }) });
    if (!response.ok) throw new Error(`Anthropic returned HTTP ${response.status}.`);
    let visibleChunks = 0;
    await readSse(response, async (data) => { const text = data.delta?.text || ""; if (text) { visibleChunks += 1; await onChunk(text); } });
    if (!visibleChunks) throw new Error("Anthropic completed without visible content.");
    return { visibleChunks };
  }
}

class GoogleGenerateAdapter extends BaseHttpAdapter {
  headers(profile) { const secret = this.secret(profile); return { Accept: "application/json", "Content-Type": "application/json", ...(profile.kind === "vertex" ? { Authorization: `Bearer ${secret}` } : {}) }; }
  keyed(profile, path) { const secret = this.secret(profile); return profile.kind === "vertex" ? `${this.base(profile)}${path}` : `${this.base(profile)}${path}${path.includes("?") ? "&" : "?"}key=${encodeURIComponent(secret)}`; }
  async probe(profile) {
    if (profile.kind === "vertex") return profile.modelId ? [{ id: profile.modelId, name: profile.modelId }] : [];
    const response = await this.registry.fetchImpl(this.keyed(profile, "/models"), { headers: this.headers(profile), signal: AbortSignal.timeout(8000) });
    if (!response.ok) throw new Error(`Google returned HTTP ${response.status}.`);
    return normalizeModels((await response.json()).models || []);
  }
  async stream(profile, request, onChunk, signal) {
    const model = request.modelId || profile.modelId;
    const contents = (request.messages || []).filter((item) => item.role !== "system").map((item) => ({ role: item.role === "assistant" ? "model" : "user", parts: [{ text: String(item.content || "") }] }));
    const system = (request.messages || []).find((item) => item.role === "system");
    const body = { contents, generationConfig: { maxOutputTokens: this.outputLimit(request) }, ...(system ? { systemInstruction: { parts: [{ text: String(system.content || "") }] } } : {}) };
    const response = await this.registry.fetchImpl(this.keyed(profile, `/models/${encodeURIComponent(model)}:streamGenerateContent?alt=sse`), { method: "POST", headers: this.headers(profile), signal, body: JSON.stringify(body) });
    if (!response.ok) throw new Error(`Google returned HTTP ${response.status}.`);
    let visibleChunks = 0;
    await readSse(response, async (data) => { for (const part of data.candidates?.[0]?.content?.parts || []) if (part.text) { visibleChunks += 1; await onChunk(part.text); } });
    if (!visibleChunks) throw new Error("Google completed without visible content.");
    return { visibleChunks };
  }
}

class AzureOpenAiAdapter extends OpenAiCompatibleAdapter {
  headers(profile, json = false) { const headers = super.headers({ ...profile, kind: "lm-studio" }, json); delete headers.Authorization; headers["api-key"] = this.secret(profile); return headers; }
  async probe(profile) {
    if (!profile.modelId) throw new Error("Azure OpenAI requires a deployment name in Model.");
    const chunks = [];
    await this.stream(profile, { modelId: profile.modelId, messages: [{ role: "user", content: "Reply OK" }], maxOutputTokens: 1 }, (chunk) => chunks.push(chunk), AbortSignal.timeout(8000));
    return [{ id: profile.modelId, name: profile.modelId }];
  }
  async stream(profile, request, onChunk, signal) {
    const inferencePath = profile.inferencePath || "/chat/completions?api-version=2024-10-21";
    return super.stream({ ...profile, inferencePath }, request, onChunk, signal);
  }
}

class AwsBedrockAdapter extends BaseHttpAdapter {
  config(profile) {
    const secret = JSON.parse(this.secret(profile) || "{}");
    if (!secret.accessKeyId || !secret.secretAccessKey) throw new Error("AWS Bedrock credential must be JSON containing accessKeyId and secretAccessKey.");
    return { region: profile.baseUrl || secret.region || "us-east-1", credentials: { accessKeyId: secret.accessKeyId, secretAccessKey: secret.secretAccessKey, sessionToken: secret.sessionToken } };
  }
  async probe(profile) {
    const { BedrockClient, ListFoundationModelsCommand } = await import("@aws-sdk/client-bedrock");
    const result = await new BedrockClient(this.config(profile)).send(new ListFoundationModelsCommand({}));
    return normalizeModels((result.modelSummaries || []).map((item) => ({ id: item.modelId, name: item.modelName })));
  }
  async stream(profile, request, onChunk, signal) {
    const { BedrockRuntimeClient, ConverseStreamCommand } = await import("@aws-sdk/client-bedrock-runtime");
    const messages = (request.messages || []).filter((item) => item.role !== "system").map((item) => ({ role: item.role === "assistant" ? "assistant" : "user", content: [{ text: String(item.content || "") }] }));
    const system = (request.messages || []).filter((item) => item.role === "system").map((item) => ({ text: String(item.content || "") }));
    const response = await new BedrockRuntimeClient(this.config(profile)).send(new ConverseStreamCommand({ modelId: request.modelId || profile.modelId, messages, system, inferenceConfig: { maxTokens: this.outputLimit(request) } }), { abortSignal: signal });
    let visibleChunks = 0;
    for await (const event of response.stream || []) { const text = event.contentBlockDelta?.delta?.text || ""; if (text) { visibleChunks += 1; await onChunk(text); } }
    if (!visibleChunks) throw new Error("AWS Bedrock completed without visible content.");
    return { visibleChunks };
  }
}

function normalizeModels(items) {
  return (Array.isArray(items) ? items : []).map((item) => ({ id: String(item.id || item.name || "").replace(/^models\//, ""), name: String(item.displayName || item.name || item.id || "").replace(/^models\//, "") })).filter((item) => item.id);
}

async function readSse(response, onData) {
  if (!response.body) throw new Error("Provider returned no stream.");
  const decoder = new TextDecoder(); let buffer = "";
  for await (const bytes of response.body) {
    buffer += decoder.decode(bytes, { stream: true });
    const events = buffer.split(/\r?\n\r?\n/); buffer = events.pop() || "";
    for (const event of events) for (const line of event.split(/\r?\n/)) {
      if (!line.startsWith("data:")) continue;
      const value = line.slice(5).trim();
      if (value && value !== "[DONE]") await onData(JSON.parse(value));
    }
  }
}
