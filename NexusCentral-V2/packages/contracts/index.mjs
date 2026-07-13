export const API_VERSION = 1;
export const DEFAULT_CONTEXT_LENGTH = 16000;
export const PROVIDER_TEMPLATES = Object.freeze({
  openrouter: provider("OpenRouter", "openai-chat", "https://openrouter.ai/api/v1", "bearer"),
  openai: provider("OpenAI", "openai-chat", "https://api.openai.com/v1", "bearer"),
  "nvidia-nim": provider("NVIDIA NIM", "openai-chat", "https://integrate.api.nvidia.com/v1", "bearer", { healthPath: "/health/ready" }),
  "lm-studio": provider("LM Studio", "openai-chat", "http://127.0.0.1:1234/v1", "none", { local: true }),
  ollama: provider("Ollama", "openai-chat", "http://127.0.0.1:11434/v1", "none", { local: true }),
  llamacpp: provider("llama.cpp", "openai-chat", "http://127.0.0.1:8080/v1", "none", { local: true }),
  vllm: provider("vLLM", "openai-chat", "http://127.0.0.1:8000/v1", "none", { local: true }),
  localai: provider("LocalAI", "openai-chat", "http://127.0.0.1:8080/v1", "none", { local: true }),
  groq: provider("Groq", "openai-chat", "https://api.groq.com/openai/v1", "bearer"),
  together: provider("Together AI", "openai-chat", "https://api.together.xyz/v1", "bearer"),
  fireworks: provider("Fireworks AI", "openai-chat", "https://api.fireworks.ai/inference/v1", "bearer"),
  mistral: provider("Mistral AI", "openai-chat", "https://api.mistral.ai/v1", "bearer"),
  cerebras: provider("Cerebras", "openai-chat", "https://api.cerebras.ai/v1", "bearer"),
  deepinfra: provider("DeepInfra", "openai-chat", "https://api.deepinfra.com/v1/openai", "bearer"),
  google: provider("Google Gemini", "openai-chat", "https://generativelanguage.googleapis.com/v1beta/openai", "bearer"),
  "google-native": provider("Google Gemini native", "google-generate", "https://generativelanguage.googleapis.com/v1beta", "query-key"),
  anthropic: provider("Anthropic", "anthropic-messages", "https://api.anthropic.com/v1", "anthropic-key"),
  "azure-openai": provider("Azure OpenAI", "azure-openai", "", "api-key", { modelsPath: "" }),
  "aws-bedrock": provider("AWS Bedrock", "aws-bedrock", "", "aws-credentials", { modelsPath: "" }),
  vertex: provider("Google Vertex AI", "google-generate", "", "bearer", { modelsPath: "" }),
  custom: provider("OpenAI-compatible", "openai-chat", "", "optional-bearer")
});

function provider(name, protocol, baseUrl, authMode, overrides = {}) {
  return Object.freeze({
    name, protocol, baseUrl, authMode,
    apiKeyRequired: !["none", "optional-bearer"].includes(authMode),
    modelsPath: "/models", healthPath: "", inferencePath: "/chat/completions",
    contextLength: DEFAULT_CONTEXT_LENGTH,
    capabilities: ["text-generation", "streaming", "cancellation"],
    local: false,
    ...overrides
  });
}

export function commandEnvelope(command, payload = {}, options = {}) {
  return {
    version: API_VERSION,
    requestId: options.requestId || crypto.randomUUID(),
    idempotencyKey: options.idempotencyKey || crypto.randomUUID(),
    deadlineUtc: options.deadlineUtc || new Date(Date.now() + 30_000).toISOString(),
    command,
    payload
  };
}

export function assertEnvelope(value) {
  if (!value || value.version !== API_VERSION || typeof value.command !== "string") throw new Error("Invalid CentralClient command envelope.");
  if (!value.requestId || !value.idempotencyKey) throw new Error("Command identifiers are required.");
  if (Date.parse(value.deadlineUtc) <= Date.now()) throw new Error("Command deadline has expired.");
  return value;
}

export function publicError(error, code = "INTERNAL_ERROR") {
  return { code, message: error instanceof Error ? error.message : String(error), retryable: false };
}
