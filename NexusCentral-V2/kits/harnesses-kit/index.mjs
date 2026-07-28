import { createStateKit } from "../_shared/create-state-kit.mjs";

const commonOptions = ["model", "profile", "oss", "localProvider", "images", "outputSchema", "strictConfig"];
const profiles = [
  { id: "conversation-harness", name: "Conversation", version: "2.0.0", executor: "direct-inference", stages: ["respond"], tools: [], capabilities: ["conversation"], allowedSandboxes: ["read-only"], allowedOptions: ["model"], defaultSandbox: "read-only", maxAttempts: 1 },
  { id: "review-harness", name: "Review only", version: "2.0.0", executor: "codex-cli", stages: ["review"], tools: ["workspace.read"], capabilities: ["review"], allowedSandboxes: ["read-only"], allowedOptions: commonOptions, defaultSandbox: "read-only", maxAttempts: 1 },
  { id: "coding-harness", name: "Coding loop", version: "2.0.0", executor: "codex-cli", stages: ["review", "act", "audit"], tools: ["workspace.read", "workspace.write"], capabilities: ["coding", "review-act-audit"], allowedSandboxes: ["read-only", "workspace-write"], allowedOptions: commonOptions, defaultSandbox: "workspace-write", maxAttempts: 2 },
  { id: "audit-harness", name: "Audit only", version: "2.0.0", executor: "codex-cli", stages: ["audit"], tools: ["workspace.read"], capabilities: ["audit"], allowedSandboxes: ["read-only"], allowedOptions: commonOptions, defaultSandbox: "read-only", maxAttempts: 1 }
];

function publicProfile(payload) {
  return {
    id: String(payload.id), name: String(payload.name), version: String(payload.version || "1.0.0"),
    executor: String(payload.executor || "codex-cli"), stages: payload.stages || [], tools: payload.tools || [],
    capabilities: payload.capabilities || [], allowedSandboxes: payload.allowedSandboxes || ["read-only"],
    allowedOptions: payload.allowedOptions || commonOptions, defaultSandbox: String(payload.defaultSandbox || "read-only"),
    maxAttempts: Math.max(1, Math.min(3, Number(payload.maxAttempts || 1)))
  };
}

export function createCentralHarnessesKit() {
  return createStateKit({
    id: "n-central-harnesses-kit", domain: "harnesses", domainPath: "n:central:harnesses", apiName: "centralHarnesses",
    name: "NexusCentral Harnesses", summary: "Owns safe harness profiles, capabilities, executor selection, and run configuration.",
    requires: ["n:central"], outputs: ["harnesses.changed"], resetPolicy: "select Conversation and clear run overrides",
    initialState: { profiles, selectedHarnessId: "conversation-harness", configurations: {} },
    transitions: {
      importProfile(state, payload) {
        const profile = publicProfile(payload);
        if (!profile.id || !profile.stages.length) throw new Error("Harness profile requires id and stages.");
        state.profiles = [...state.profiles.filter((item) => item.id !== profile.id), profile];
        return { state, result: profile };
      },
      select(state, payload) {
        if (!state.profiles.some((item) => item.id === payload.id)) throw new Error("Unknown harness profile.");
        state.selectedHarnessId = String(payload.id);
        return { state, result: state.profiles.find((item) => item.id === payload.id) };
      },
      configure(state, payload) {
        const profile = state.profiles.find((item) => item.id === payload.id);
        if (!profile) throw new Error("Unknown harness profile.");
        state.configurations[payload.id] = {
          model: String(payload.model || ""), profile: String(payload.profile || ""), sandbox: String(payload.sandbox || profile.defaultSandbox),
          oss: Boolean(payload.oss), localProvider: String(payload.localProvider || ""), images: payload.images || [],
          outputSchema: String(payload.outputSchema || ""), strictConfig: Boolean(payload.strictConfig), validationCommands: payload.validationCommands || [], simulatorTarget: String(payload.simulatorTarget || "")
        };
        return { state, result: state.configurations[payload.id] };
      }
    },
    reset(_state, empty) { return empty; }
  });
}
