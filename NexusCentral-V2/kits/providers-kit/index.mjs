import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralProvidersKit() {
  return createStateKit({
    id: "n-central-providers-kit", domain: "providers", domainPath: "n:central:providers", apiName: "centralProviders",
    name: "NexusCentral Providers", summary: "Owns secret-free provider profiles, health, and selection.",
    requires: ["n:central", "n:central:audit"], outputs: ["providers.changed", "providers.health-observed"], resetPolicy: "clear active selection and transient health",
    initialState: { profiles: [], activeProviderId: null },
    transitions: {
      upsert(state, payload) {
        const profile = {
          id: String(payload.id), kind: String(payload.kind), protocol: String(payload.protocol || "openai-chat"),
          name: String(payload.name), baseUrl: String(payload.baseUrl), modelId: String(payload.modelId || ""),
          authMode: String(payload.authMode || "none"), secretRef: String(payload.secretRef || ""),
          modelsPath: String(payload.modelsPath || ""), healthPath: String(payload.healthPath || ""), inferencePath: String(payload.inferencePath || ""),
          contextLength: Number(payload.contextLength || 16000), capabilities: payload.capabilities || ["text-generation", "streaming", "cancellation"],
          local: payload.local === true, enabled: payload.enabled !== false,
          sharePolicy: {
            enabled: payload.sharePolicy?.enabled === true,
            concurrencyLimit: Math.max(1, Math.min(16, Number(payload.sharePolicy?.concurrencyLimit || 1))),
            tokensPerMinute: Math.max(1000, Math.min(1_000_000, Number(payload.sharePolicy?.tokensPerMinute || 16000)))
          },
          health: "inactive", models: []
        };
        state.profiles = [...state.profiles.filter((item) => item.id !== profile.id), profile];
        return { state, result: profile };
      },
      observeHealth(state, payload) {
        state.profiles = state.profiles.map((item) => item.id === payload.id ? { ...item, health: String(payload.health), models: payload.models || [], lastProbeUtc: payload.lastProbeUtc || null } : item);
        return { state };
      },
      select(state, payload) {
        const id = String(payload.id || "");
        if (id && !state.profiles.some((item) => item.id === id)) throw new Error("Unknown provider.");
        state.activeProviderId = id || null;
        return { state };
      },
      setModel(state, payload) {
        if (!state.profiles.some((item) => item.id === payload.id)) throw new Error("Unknown provider.");
        state.profiles = state.profiles.map((item) => item.id === payload.id ? { ...item, modelId: String(payload.modelId) } : item);
        return { state };
      },
      setSharePolicy(state, payload) {
        if (!state.profiles.some((item) => item.id === payload.id)) throw new Error("Unknown provider.");
        const sharePolicy = { enabled: payload.enabled === true, concurrencyLimit: Math.max(1, Math.min(16, Number(payload.concurrencyLimit || 1))), tokensPerMinute: Math.max(1000, Math.min(1_000_000, Number(payload.tokensPerMinute || 16000))) };
        state.profiles = state.profiles.map((item) => item.id === payload.id ? { ...item, sharePolicy } : item);
        return { state, result: sharePolicy };
      }
    },
    reset(state, empty) {
      return { ...empty, profiles: state.profiles.map((item) => ({ ...item, health: "inactive", models: [], lastProbeUtc: null })) };
    }
  });
}
