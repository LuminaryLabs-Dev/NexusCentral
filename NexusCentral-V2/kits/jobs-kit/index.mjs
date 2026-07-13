import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralJobsKit() {
  return createStateKit({
    id: "n-central-jobs-kit", domain: "jobs", domainPath: "n:central:jobs", apiName: "centralJobs",
    name: "NexusCentral Jobs", summary: "Owns durable queue state, leases, retries, cancellation, and outcomes.",
    requires: ["n:central:routing", "n:central:policy", "n:central:audit"], outputs: ["jobs.changed", "jobs.progress"], resetPolicy: "requeue recoverable active jobs without replaying completed effects",
    initialState: { jobs: [] },
    transitions: {
      enqueue(state, payload) {
        if (state.jobs.some((item) => item.idempotencyKey === payload.idempotencyKey)) return { state, result: state.jobs.find((item) => item.idempotencyKey === payload.idempotencyKey) };
        const job = { id: String(payload.id), idempotencyKey: String(payload.idempotencyKey), kind: String(payload.kind), status: "queued", payload: payload.payload || {}, attempts: 0, route: null, lease: null, progress: [], result: null, error: null, createdAt: Number(payload.createdAt) };
        state.jobs.push(job);
        return { state, result: job };
      },
      claim(state, payload) {
        const job = state.jobs.find((item) => item.id === payload.id);
        if (!job || !["queued", "retry"].includes(job.status)) throw new Error("Job is not claimable.");
        job.status = "running"; job.attempts += 1; job.route = payload.route; job.lease = { ownerId: String(payload.ownerId), expiresAt: Number(payload.expiresAt) };
        return { state, result: job };
      },
      progress(state, payload) {
        const job = state.jobs.find((item) => item.id === payload.id);
        if (!job || job.status !== "running") throw new Error("Job is not running.");
        job.progress.push({ sequence: Number(payload.sequence), content: String(payload.content || ""), at: Number(payload.at) });
        return { state, result: job };
      },
      reroute(state, payload) {
        const job = state.jobs.find((item) => item.id === payload.id);
        if (!job || job.status !== "running") throw new Error("Job is not running.");
        job.route = payload.route;
        return { state, result: job };
      },
      retry(state, payload) {
        const job = state.jobs.find((item) => item.id === payload.id);
        if (!job || ["completed", "cancelled"].includes(job.status)) return { state, result: job || null };
        job.status = "retry"; job.error = String(payload.error || "Route unavailable."); job.lease = null; job.route = null;
        return { state, result: job };
      },
      finish(state, payload) {
        const job = state.jobs.find((item) => item.id === payload.id);
        if (!job) throw new Error("Unknown job.");
        job.status = String(payload.status || "completed"); job.result = payload.result || null; job.error = payload.error || null; job.lease = null;
        return { state, result: job };
      },
      cancel(state, payload) {
        const job = state.jobs.find((item) => item.id === payload.id);
        if (!job || ["completed", "failed", "cancelled"].includes(job.status)) return { state, result: job || null };
        job.status = "cancelled"; job.lease = null;
        return { state, result: job };
      },
      requeueExpired(state, payload) {
        for (const job of state.jobs) if (job.status === "running" && job.lease?.expiresAt <= Number(payload.now)) { job.status = "retry"; job.lease = null; }
        return { state };
      },
      recoverRunning(state) {
        for (const job of state.jobs) if (job.status === "running") { job.status = "retry"; job.lease = null; job.error = "Previous Nexus Node stopped before this job completed."; }
        return { state };
      }
    },
    reset(state, empty) {
      return { ...empty, jobs: state.jobs.map((job) => job.status === "running" ? { ...job, status: "retry", lease: null } : job) };
    }
  });
}
