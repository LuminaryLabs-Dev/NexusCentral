import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralModelCatalogKit() {
  return createStateKit({
    id: "n-central-model-catalog-kit", domain: "model-catalog", domainPath: "n:central:model-catalog", apiName: "centralModelCatalog",
    name: "NexusCentral Model Catalog", summary: "Owns model manifests, formats, hashes, and availability.",
    requires: ["n:central", "n:central:audit"], outputs: ["model-catalog.changed"], resetPolicy: "clear transient availability while preserving manifests",
    initialState: { models: [] },
    transitions: {
      register(state, payload) {
        const model = { id: String(payload.id), name: String(payload.name || payload.id), source: String(payload.source || "local"), format: String(payload.format || "unknown"), hash: String(payload.hash || ""), contextLength: Number(payload.contextLength || 16000), availability: String(payload.availability || "available") };
        state.models = [...state.models.filter((item) => item.id !== model.id), model];
        return { state, result: model };
      },
      setAvailability(state, payload) {
        state.models = state.models.map((item) => item.id === payload.id ? { ...item, availability: String(payload.availability) } : item);
        return { state };
      }
    },
    reset(state, empty) {
      return { ...empty, models: state.models.map((item) => ({ ...item, availability: "unknown" })) };
    }
  });
}
