import { createStateKit } from "../_shared/create-state-kit.mjs";

function score(candidate) {
  return Number(candidate.healthy !== false) * 1000
    + Number(candidate.local === true) * 100
    + Math.max(0, 50 - Number(candidate.queueDepth || 0))
    - Number(candidate.costScore || 0)
    - Number(candidate.latencyMs || 0) / 100;
}

export function createCentralRoutingKit() {
  return createStateKit({
    id: "n-central-routing-kit", domain: "routing", domainPath: "n:central:routing", apiName: "centralRouting",
    name: "NexusCentral Routing", summary: "Owns route candidates, constraints, rationale, and fallback order.",
    requires: ["n:central:providers", "n:central:model-runtime", "n:central:mesh-membership", "n:central:policy"], outputs: ["routing.selected"], resetPolicy: "clear route decisions",
    initialState: { decisions: [] },
    transitions: {
      select(state, payload) {
        const contextLength = Number(payload.contextLength || 16000);
        const candidates = (payload.candidates || [])
          .filter((item) => item.healthy !== false && Number(item.contextLength || 0) >= contextLength)
          .map((item) => ({ ...item, score: score(item) }))
          .sort((left, right) => right.score - left.score || String(left.id).localeCompare(String(right.id)));
        if (!candidates.length) throw new Error("No compatible inference route is available.");
        const decision = { requestId: String(payload.requestId), selected: candidates[0], fallbacks: candidates.slice(1), contextLength, rationale: `Selected ${candidates[0].id} by health, locality, capacity, queue, cost, and latency.` };
        state.decisions.push(decision);
        return { state, result: decision };
      }
    }
  });
}
