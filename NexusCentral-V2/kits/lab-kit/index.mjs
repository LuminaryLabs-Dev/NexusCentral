import { createStateKit } from "../_shared/create-state-kit.mjs";

const transitions = { review: "act", act: "audit", audit: "completed" };

export function createCentralLabKit() {
  return createStateKit({
    id: "n-central-lab-kit", domain: "lab", domainPath: "n:central:lab", apiName: "centralLab",
    name: "NexusCentral Lab", summary: "Owns the Review, Act, Audit run state machine.",
    requires: ["n:central:workspace", "n:central:harnesses", "n:central:jobs", "n:central:proof"], outputs: ["lab.changed"], resetPolicy: "clear active run while preserving completed run summaries",
    initialState: { activeRun: null, runs: [] },
    transitions: {
      start(state, payload) {
        if (state.activeRun) throw new Error("A Lab run is already active.");
        state.activeRun = { id: String(payload.id), task: String(payload.task), workspaceId: String(payload.workspaceId), harnessId: String(payload.harnessId), stage: "review", status: "running", stageJobs: [], startedAt: Number(payload.startedAt) };
        return { state, result: state.activeRun };
      },
      attachJob(state, payload) {
        if (!state.activeRun || state.activeRun.id !== payload.id) throw new Error("Unknown active Lab run.");
        state.activeRun.stageJobs.push({ stage: state.activeRun.stage, jobId: String(payload.jobId) });
        return { state, result: state.activeRun };
      },
      advance(state, payload) {
        if (!state.activeRun || state.activeRun.id !== payload.id) throw new Error("Unknown active Lab run.");
        if (!payload.ok) { state.activeRun.status = "failed"; state.activeRun.error = String(payload.error || "Stage failed."); return { state, result: state.activeRun }; }
        state.activeRun.stage = transitions[state.activeRun.stage];
        if (state.activeRun.stage === "completed") state.activeRun.status = "completed";
        return { state, result: state.activeRun };
      },
      finish(state, payload) {
        if (!state.activeRun || state.activeRun.id !== payload.id) throw new Error("Unknown active Lab run.");
        const completed = { ...state.activeRun, status: String(payload.status || state.activeRun.status), finishedAt: Number(payload.finishedAt) };
        state.runs.push(completed); state.activeRun = null;
        return { state, result: completed };
      }
    },
    reset(state, empty) {
      return { ...empty, runs: state.runs };
    }
  });
}
