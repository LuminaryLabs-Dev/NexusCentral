import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralExperimentsKit() {
  return createStateKit({
    id: "n-central-experiments-kit", domain: "experiments", domainPath: "n:central:experiments", apiName: "centralExperiments",
    name: "NexusCentral Experiments", summary: "Owns disposable harness run lifecycle, evidence, approval, and promotion state.",
    requires: ["n:central:harnesses", "n:central:workspace", "n:central:jobs", "n:central:proof", "n:central:audit"],
    outputs: ["experiments.changed"], resetPolicy: "retain completed evidence summaries and clear active experiments",
    initialState: { experiments: [] },
    transitions: {
      start(state, payload) {
        const experiment = { id: String(payload.id), task: String(payload.task), workspaceId: String(payload.workspaceId), sourceRevision: Number(payload.sourceRevision), baselineHash: String(payload.baselineHash), harnessId: String(payload.harnessId), configuration: payload.configuration || {}, status: "running", stage: "prepare", attempt: 1, maxAttempts: Number(payload.maxAttempts || 1), stages: [], validation: null, artifacts: [], diff: null, approval: null, promotion: null, startedAt: Number(payload.startedAt) };
        state.experiments.push(experiment); return { state, result: experiment };
      },
      stage(state, payload) {
        const run = required(state, payload.id); run.stage = String(payload.stage); run.stages.push({ stage: run.stage, status: String(payload.status), attempt: Number(payload.attempt || run.attempt), artifact: String(payload.artifact || ""), error: String(payload.error || "") }); return { state, result: run };
      },
      validate(state, payload) {
        const run = required(state, payload.id); run.validation = payload.validation; run.artifacts = [...new Set([...run.artifacts, ...(payload.artifacts || [])])]; return { state, result: run };
      },
      retry(state, payload) {
        const run = required(state, payload.id); if (run.attempt >= run.maxAttempts) throw new Error("Experiment retry budget is exhausted."); run.attempt += 1; run.stage = "act"; return { state, result: run };
      },
      complete(state, payload) {
        const run = required(state, payload.id); run.status = "review"; run.stage = "completed"; run.diff = payload.diff; run.artifacts = [...new Set([...run.artifacts, ...(payload.artifacts || [])])]; run.finishedAt = Number(payload.finishedAt); return { state, result: run };
      },
      fail(state, payload) {
        const run = required(state, payload.id); run.status = "failed"; run.stage = "failed"; run.error = String(payload.error); run.finishedAt = Number(payload.finishedAt); return { state, result: run };
      },
      approve(state, payload) {
        const run = required(state, payload.id); if (run.status !== "review" || !run.diff?.hash) throw new Error("Only a completed experiment with a diff can be approved."); run.approval = { approvedBy: String(payload.approvedBy), approvedAt: Number(payload.approvedAt), sourceRevision: Number(payload.sourceRevision), diffHash: String(payload.diffHash) }; run.status = "approved"; return { state, result: run };
      },
      promote(state, payload) {
        const run = required(state, payload.id); if (run.status !== "approved" || run.approval?.diffHash !== payload.diffHash) throw new Error("Experiment approval does not match this diff."); run.status = "promoted"; run.promotion = { promotedBy: String(payload.promotedBy), promotedAt: Number(payload.promotedAt), sourceRevision: Number(payload.sourceRevision) }; return { state, result: run };
      },
      recoverRunning(state, payload) {
        for (const run of state.experiments) if (run.status === "running") { run.status = "failed"; run.stage = "failed"; run.error = String(payload.error || "Nexus Node stopped during the experiment. Evidence was retained."); }
        return { state };
      }
    },
    reset(state, empty) { return { ...empty, experiments: state.experiments.filter((item) => ["review", "approved", "promoted", "failed"].includes(item.status)) }; }
  });
}

function required(state, id) {
  const run = state.experiments.find((item) => item.id === id);
  if (!run) throw new Error("Unknown experiment.");
  return run;
}
