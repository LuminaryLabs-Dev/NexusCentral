import { randomUUID } from "node:crypto";

export class RunCoordinator {
  constructor(node) { this.node = node; }

  submit(payload, context, source = "runs.submit") {
    const idempotencyKey = String(payload.idempotencyKey || payload.requestId || "");
    const existingJob = idempotencyKey ? this.node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.idempotencyKey === idempotencyKey) : null;
    if (existingJob) {
      this.node.requireScope(context, existingJob.kind === "inference" ? "view" : "workspace.operate");
      const existingExperiment = this.node.engine.n.centralExperiments.getSnapshot().experiments.find((item) => item.id === existingJob.id);
      return { submitted: existingExperiment || existingJob, run: this.get(existingJob.id) };
    }
    const harnesses = this.node.engine.n.centralHarnesses.getSnapshot();
    const forceInference = source === "inference.submit" || payload.kind === "inference";
    const harnessId = forceInference ? "conversation-harness" : String(payload.harnessId || harnesses.selectedHarnessId);
    const profile = harnesses.profiles.find((item) => item.id === harnessId);
    if (!profile) throw new Error("Unknown harness profile.");
    this.node.requireScope(context, profile.executor === "direct-inference" ? "view" : "workspace.operate");
    const configuration = { ...(harnesses.configurations[harnessId] || {}), ...(payload.configuration || {}) };
    this.node.harnessExecutors.assertProfile(profile, configuration);
    const id = String(payload.runId || randomUUID());
    const submitted = profile.executor === "direct-inference"
      ? this.node.submitInferenceRun({ ...payload, messages: messages(payload), harnessId }, context, { id, profile, configuration })
      : this.node.submitExperimentRun(payload, context, { id, profile, configuration });
    return { submitted, run: this.get(id) };
  }

  get(id) {
    const job = this.node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === id);
    if (!job) return null;
    const experiment = this.node.engine.n.centralExperiments.getSnapshot().experiments.find((item) => item.id === id) || null;
    const artifacts = this.node.engine.n.centralArtifacts.getSnapshot().artifacts.filter((item) => item.runId === id || item.jobId === id);
    return { id: job.id, kind: job.kind, status: canonicalStatus(job.status), createdAt: job.createdAt, updatedAt: job.updatedAt, route: job.route, scope: job.scope, result: job.result, error: job.error, experiment, artifacts };
  }

  list() { return this.node.engine.n.centralJobs.getSnapshot().jobs.map((job) => this.get(job.id)); }
  cancel(id) { return this.node.cancelRun(id); }
}

function messages(payload) { return payload.messages?.length ? payload.messages : [{ role: "user", content: String(payload.task || payload.prompt || "") }]; }
function canonicalStatus(status) { return status === "retry" ? "queued" : status === "cancelled" ? "canceled" : status; }
