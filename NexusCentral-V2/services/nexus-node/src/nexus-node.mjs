import { EventEmitter } from "node:events";
import { createHash, randomBytes, randomUUID } from "node:crypto";
import { dirname, join, relative, resolve } from "node:path";
import { DEFAULT_CONTEXT_LENGTH, PROVIDER_TEMPLATES, assertEnvelope } from "@nexuscentral/contracts";
import { FilesystemWorkspaceAdapter, Libp2pMeshAdapter, MemorySecretStore, MeshTransportCoordinator, ModelLifecycleAdapter, OsSecretStore, PeerJsRoomAdapter, ProcessHarnessAdapter, ProviderAdapterRegistry, SimSpaceExperimentAdapter, SqliteStateStore } from "@nexuscentral/runtime-adapters";
import { createCentralRuntime, getCentralGraphSnapshot } from "../../../packages/composition/index.mjs";

const SNAPSHOT_APIS = {
  "n:central": "central", "n:central:node": "centralNode", "n:central:audit": "centralAudit", "n:central:policy": "centralPolicy",
  "n:central:workspace": "centralWorkspace", "n:central:artifacts": "centralArtifacts", "n:central:providers": "centralProviders",
  "n:central:model-catalog": "centralModelCatalog", "n:central:model-runtime": "centralModelRuntime", "n:central:mesh-membership": "centralMeshMembership",
  "n:central:routing": "centralRouting", "n:central:harnesses": "centralHarnesses", "n:central:jobs": "centralJobs",
  "n:central:inference": "centralInference", "n:central:workspace-transfer": "centralWorkspaceTransfer", "n:central:proof": "centralProof",
  "n:central:lab": "centralLab", "n:central:experiments": "centralExperiments", "n:central:lan-access": "centralLanAccess"
};

function publicProvider({ secretRef, ...profile }, admin = false) {
  const visible = admin ? profile : { id: profile.id, kind: profile.kind, name: profile.name, modelId: profile.modelId, health: profile.health, lastProbeUtc: profile.lastProbeUtc };
  return { ...visible, hasCredential: Boolean(secretRef) };
}

function publicHarness(profile) { return profile; }

export class NexusNode extends EventEmitter {
  constructor({ databasePath, secretStore, fetchImpl = fetch, now = () => Date.now(), harnessProcess, experimentAdapter, simulatorCli, meshCoordinator } = {}) {
    super();
    this.now = now;
    this.engine = createCentralRuntime({ node: { nodeId: `node-${randomUUID()}` } });
    this.store = new SqliteStateStore(databasePath);
    this.secretStore = secretStore || (process.env.NODE_ENV === "test" ? new MemorySecretStore() : new OsSecretStore());
    this.providers = new ProviderAdapterRegistry({ secretStore: this.secretStore, fetchImpl });
    this.models = new ModelLifecycleAdapter({ fetchImpl });
    this.harnessProcess = harnessProcess || new ProcessHarnessAdapter();
    this.experimentAdapter = experimentAdapter || new SimSpaceExperimentAdapter({ runRoot: join(dirname(databasePath), "experiments"), simulatorCli: simulatorCli || resolve(import.meta.dirname, "../../../../../NexusSimulator/NexusSimulator-V1/src/cli.js") });
    this.filesystem = new FilesystemWorkspaceAdapter();
    this.restore();
    this.mesh = meshCoordinator || new MeshTransportCoordinator({
      lan: new Libp2pMeshAdapter({ secretStore: this.secretStore }),
      peerjs: new PeerJsRoomAdapter({ nodeId: this.engine.n.centralNode.getSnapshot().nodeId, fetchImpl })
    });
    this.abortControllers = new Map();
    this.remoteUsage = new Map();
    this.browserDispatcher = null;
    this.mesh.setRequestHandler((request, peer, stream) => this.#handleMeshRequest(request, peer, stream));
    this.mesh.on("peer", (peer) => this.#observeMeshPeer(peer));
    this.mesh.on("transport-error", ({ transport, error }) => this.#emit("mesh.transport-error", { transport, error: error.message }));
    this.engine.n.central.setReadiness("ready", "node-runtime-ready");
    queueMicrotask(() => this.resumeRetryJobs());
  }

  restore() {
    const snapshots = this.store.loadAll();
    for (const [domainPath, snapshot] of Object.entries(snapshots)) {
      const apiName = SNAPSHOT_APIS[domainPath];
      if (apiName && this.engine.n[apiName]) this.engine.n[apiName].loadSnapshot(snapshot);
    }
    this.engine.n.centralJobs.recoverRunning({ requestId: `restore:${this.now()}` });
    this.engine.n.centralExperiments.recoverRunning({ error: "Nexus Node stopped during the experiment. Evidence was retained.", requestId: `experiment-restore:${this.now()}` });
    const harnesses = this.engine.n.centralHarnesses.getSnapshot();
    if (!harnesses.profiles.some((profile) => profile.id === harnesses.selectedHarnessId)) this.engine.n.centralHarnesses.select({ id: harnesses.selectedHarnessId === "codex-cli-harness" ? "coding-harness" : "conversation-harness", requestId: `harness-migrate:${this.now()}` });
    this.engine.n.centralModelRuntime.expire({ now: this.now(), requestId: `runtime-expire:${this.now()}` });
    this.engine.n.centralLanAccess.expire({ now: this.now(), requestId: `session-expire:${this.now()}` });
    const workspace = this.engine.n.centralWorkspace.getSnapshot().selected;
    const workspacePath = this.store.getHostValue("workspace.path");
    if (workspace && workspacePath) {
      try { this.filesystem.open(workspacePath, workspace.id); } catch (_error) { this.engine.n.centralWorkspace.reset(); }
    }
  }

  persist() {
    for (const [domainPath, apiName] of Object.entries(SNAPSHOT_APIS)) this.store.save(domainPath, this.engine.n[apiName].getSnapshot());
  }

  capabilities() {
    return { apiVersion: 1, product: "Nexus Central Web", contextLength: DEFAULT_CONTEXT_LENGTH, surfaces: ["lab", "router", "models", "review", "settings"], inferenceRoutes: ["provider", "local-runtime", "remote-peer", "browser-onnx"], meshTransports: ["lan", "peerjs"], browserOnnxTasks: ["tensor"], hostAuthoritative: true };
  }

  publicState(context = { actorId: "anonymous", scopes: [] }) {
    const graph = getCentralGraphSnapshot(this.engine);
    const admin = context.scopes?.includes("admin");
    const profiles = graph.providers.profiles.map((profile) => publicProvider(profile, admin));
    const harnessProfiles = graph.harnesses.profiles.map(publicHarness);
    const { manifests: _legacyManifests, ...harnessState } = graph.harnesses;
    const runtimes = graph.modelRuntime.runtimes.map(({ sessionId, ...runtime }) => admin ? { sessionId, ...runtime } : runtime);
    const peers = { ...graph.meshMembership, transports: this.mesh?.status?.() || {}, peers: graph.meshMembership.peers.map(({ addresses, ...peer }) => admin ? { addresses, ...peer } : peer) };
    const jobs = { ...graph.jobs, jobs: graph.jobs.jobs.map((job) => ({ ...job, payload: { ...job.payload, actorId: undefined } })) };
    const audit = { ...graph.audit, processedRequestIds: undefined, entries: graph.audit.entries.map((entry) => ({ ...entry, actorId: admin ? entry.actorId : entry.actorId === context.actorId ? "current-session" : "host-or-peer" })) };
    const sessions = graph.lanAccess.sessions
      .filter((session) => admin || session.id === context.actorId)
      .map(({ id, ...session }) => admin ? { id, ...session } : session);
    return {
      capabilities: this.capabilities(), graph: graph.paths, workspace: graph.workspace,
      providers: { ...graph.providers, profiles }, models: graph.modelCatalog, runtimes: { ...graph.modelRuntime, runtimes },
      peers, harnesses: { ...harnessState, profiles: harnessProfiles }, jobs,
      artifacts: graph.artifacts, audit, policy: admin ? graph.policy : { defaultDecision: graph.policy.defaultDecision }, routing: graph.routing, proof: graph.proof, lab: graph.lab, experiments: graph.experiments, sessions: { sessions }
    };
  }

  requireScope(context, scope) {
    if (context.scopes?.includes("admin") || context.scopes?.includes(scope)) return;
    throw new Error(`Scope ${scope} is required.`);
  }

  async execute(rawEnvelope, context = { actorId: "unknown", scopes: [] }) {
    const envelope = assertEnvelope(rawEnvelope);
    const payload = { ...envelope.payload, requestId: envelope.idempotencyKey };
    let result;
    try { switch (envelope.command) {
      case "system.state": this.requireScope(context, "view"); result = { ...this.publicState(context), actor: { id: context.actorId, scopes: context.scopes } }; break;
      case "workspace.open": this.requireScope(context, "workspace.operate"); result = this.#openWorkspace(payload); break;
      case "workspace.list": this.requireScope(context, "view"); result = this.engine.n.centralWorkspace.getSnapshot(); break;
      case "providers.list": this.requireScope(context, "view"); result = { ...this.publicState(context).providers, templates: PROVIDER_TEMPLATES }; break;
      case "providers.save": this.requireScope(context, "admin"); result = this.#saveProvider(payload); break;
      case "providers.test": this.requireScope(context, "admin"); result = await this.#testProvider(payload); break;
      case "providers.set-share-policy": this.requireScope(context, "admin"); result = this.engine.n.centralProviders.setSharePolicy(payload); break;
      case "models.register": this.requireScope(context, "admin"); result = this.engine.n.centralModelCatalog.register(payload); break;
      case "models.list": this.requireScope(context, "view"); result = this.engine.n.centralModelCatalog.getSnapshot(); break;
      case "models.list-installed": this.requireScope(context, "view"); result = await this.#listInstalledModels(); break;
      case "models.search": this.requireScope(context, "view"); result = await this.models.search(payload.query); break;
      case "models.download": this.requireScope(context, "admin"); result = await this.models.download(this.#lmStudioProfile(), payload.modelId); break;
      case "models.load": this.requireScope(context, "admin"); result = await this.#loadModel(payload); break;
      case "models.unload": this.requireScope(context, "admin"); result = await this.#unloadModel(payload); break;
      case "harnesses.import": this.requireScope(context, "admin"); result = publicHarness(this.engine.n.centralHarnesses.importProfile(payload)); break;
      case "harnesses.select": this.requireScope(context, "admin"); this.engine.n.centralHarnesses.select(payload); result = this.publicState(context).harnesses; break;
      case "harnesses.configure": this.requireScope(context, "admin"); result = this.engine.n.centralHarnesses.configure(payload); break;
      case "harnesses.discover": this.requireScope(context, "view"); result = await this.#discoverHarnessOptions(); break;
      case "harnesses.list": this.requireScope(context, "view"); result = this.publicState(context).harnesses; break;
      case "lan.create-pairing": this.requireScope(context, "admin"); result = this.#createPairing(payload); break;
      case "lan.pair": result = this.#pair(payload); break;
      case "lan.grant-scope": this.requireScope(context, "admin"); result = this.engine.n.centralLanAccess.grantScope(payload); break;
      case "lan.revoke": this.requireScope(context, "admin"); result = this.engine.n.centralLanAccess.revoke(payload); break;
      case "policy.grant": this.requireScope(context, "admin"); result = this.engine.n.centralPolicy.grant({ peerId: payload.peerId, action: payload.action, grantId: payload.grantId || payload.requestId }); break;
      case "policy.revoke": this.requireScope(context, "admin"); result = this.engine.n.centralPolicy.revoke(payload); break;
      case "browser.register-runtime": this.requireScope(context, "inference.contribute"); result = this.#registerBrowserRuntime(payload, context); break;
      case "browser.remove-runtime": this.requireScope(context, "inference.contribute"); result = this.engine.n.centralModelRuntime.remove({ ...payload, sessionId: context.actorId }); break;
      case "inference.submit": this.requireScope(context, "view"); result = this.#submitInference(payload, context); break;
      case "jobs.cancel": this.requireScope(context, "view"); result = this.#cancelJob(payload); break;
      case "lab.submit": this.requireScope(context, "workspace.operate"); result = await this.#submitExperiment(payload, context); break;
      case "experiments.approve": this.requireScope(context, "admin"); result = this.#approveExperiment(payload, context); break;
      case "experiments.promote": this.requireScope(context, "admin"); result = this.#promoteExperiment(payload, context); break;
      case "mesh.join": this.requireScope(context, "admin"); result = await this.#joinMesh(payload); break;
      case "mesh.connect": this.requireScope(context, "admin"); result = await this.#connectMesh(payload); break;
      case "mesh.disconnect": this.requireScope(context, "admin"); result = await this.#disconnectMesh(payload); break;
      case "mesh.set-share-policy": this.requireScope(context, "admin"); result = this.engine.n.centralProviders.setSharePolicy(payload); break;
      case "mesh.state": this.requireScope(context, "view"); result = { membership: this.engine.n.centralMeshMembership.getSnapshot(), transports: this.mesh.status() }; break;
      default: throw new Error(`Unknown command: ${envelope.command}`);
    } } catch (error) {
      this.engine.n.centralAudit.append({ actorId: context.actorId, command: envelope.command, outcome: "rejected", at: this.now(), error: error.message }, `audit:${envelope.requestId}:rejected`);
      this.persist(); this.#emit("command.rejected", { command: envelope.command, requestId: envelope.requestId });
      throw error;
    }
    this.engine.n.centralAudit.append({ actorId: context.actorId, command: envelope.command, outcome: "accepted", at: this.now() }, `audit:${envelope.requestId}:accepted`);
    this.persist();
    this.#emit("state.changed", { command: envelope.command, requestId: envelope.requestId });
    return result;
  }

  #openWorkspace(payload) {
    const workspaceId = String(payload.workspaceId || randomUUID());
    const opened = this.filesystem.open(payload.path, workspaceId);
    this.store.setHostValue("workspace.path", opened.canonicalPath);
    this.engine.n.centralWorkspace.select({ workspaceId, name: opened.name, revision: 0, requestId: payload.requestId });
    return { ...this.engine.n.centralWorkspace.getSnapshot(), entries: opened.entries };
  }

  #saveProvider(payload) {
    const template = PROVIDER_TEMPLATES[payload.kind] || PROVIDER_TEMPLATES.custom;
    const id = String(payload.id || payload.kind || randomUUID());
    let secretRef = String(payload.secretRef || "");
    if (payload.apiKey) { secretRef = `provider:${id}`; this.secretStore.set(secretRef, payload.apiKey); }
    return publicProvider(this.engine.n.centralProviders.upsert({ ...template, ...payload, id, name: payload.name || template.name, baseUrl: String(payload.baseUrl || template.baseUrl).replace(/\/$/, ""), secretRef, requestId: payload.requestId }), true);
  }

  async #testProvider(payload) {
    const profile = this.engine.n.centralProviders.getSnapshot().profiles.find((item) => item.id === payload.id);
    if (!profile) throw new Error("Unknown provider.");
    try {
      const models = await this.providers.probe(profile);
      this.engine.n.centralProviders.observeHealth({ id: profile.id, health: "healthy", models, lastProbeUtc: new Date().toISOString(), requestId: payload.requestId });
      this.engine.n.centralProviders.select({ id: profile.id, requestId: `${payload.requestId}:select` });
      this.resumeRetryJobs();
      return { ok: true, models };
    } catch (error) {
      this.engine.n.centralProviders.observeHealth({ id: profile.id, health: "unhealthy", models: [], lastProbeUtc: new Date().toISOString(), requestId: payload.requestId });
      throw error;
    }
  }

  #lmStudioProfile() {
    const profiles = this.engine.n.centralProviders.getSnapshot().profiles;
    const profile = profiles.find((item) => item.kind === "lm-studio");
    if (!profile) throw new Error("Configure LM Studio in Router first.");
    return profile;
  }

  async #listInstalledModels() {
    const models = await this.models.listInstalled(this.#lmStudioProfile());
    for (const model of models) this.engine.n.centralModelCatalog.register({ ...model, source: "lm-studio", format: "gguf", contextLength: DEFAULT_CONTEXT_LENGTH, availability: model.status, requestId: `installed:${model.id}:${model.status}` });
    return models;
  }

  async #loadModel(payload) {
    const profile = this.#lmStudioProfile();
    const result = await this.models.load(profile, payload.modelId);
    const instanceId = String(result.instance_id || payload.modelId);
    this.engine.n.centralModelCatalog.register({ id: payload.modelId, name: payload.modelId, source: "lm-studio", format: "gguf", contextLength: DEFAULT_CONTEXT_LENGTH, availability: "loaded", requestId: `catalog-load:${payload.requestId}` });
    this.engine.n.centralModelRuntime.register({ id: `lm-studio:${instanceId}`, kind: "lm-studio", modelId: payload.modelId, contextLength: DEFAULT_CONTEXT_LENGTH, status: "ready", requestId: payload.requestId });
    this.engine.n.centralProviders.setModel({ id: profile.id, modelId: payload.modelId, requestId: `provider-model:${payload.requestId}` });
    return { ...result, instanceId, contextLength: DEFAULT_CONTEXT_LENGTH };
  }

  async #unloadModel(payload) {
    const result = await this.models.unload(this.#lmStudioProfile(), payload.instanceId);
    this.engine.n.centralModelRuntime.remove({ id: `lm-studio:${payload.instanceId}`, requestId: payload.requestId });
    return result;
  }

  #createPairing(payload) {
    const code = String(Math.floor(100000 + Math.random() * 900000));
    const id = randomUUID();
    const codeHash = createHash("sha256").update(code).digest("hex");
    this.engine.n.centralLanAccess.createPairing({ id, codeHash, expiresAt: this.now() + 5 * 60_000, requestId: payload.requestId });
    return { pairingId: id, code, expiresAt: this.now() + 5 * 60_000 };
  }

  #pair(payload) {
    const codeHash = createHash("sha256").update(String(payload.code)).digest("hex");
    const pairingId = payload.pairingId || this.engine.n.centralLanAccess.getSnapshot().pairings.find((item) => !item.used && item.codeHash === codeHash)?.id;
    return this.engine.n.centralLanAccess.pair({ pairingId, codeHash, now: this.now(), sessionId: randomBytes(32).toString("base64url"), sessionExpiresAt: this.now() + 24 * 60 * 60_000, clientName: payload.clientName, requestId: payload.requestId });
  }

  #registerBrowserRuntime(payload, context) {
    if (!payload.smokePassed || !payload.fingerprint || !["webgpu", "wasm"].includes(payload.executionProvider)) throw new Error("Browser runtime must pass a local smoke and provide a model fingerprint.");
    const runtime = this.engine.n.centralModelRuntime.register({ ...payload, id: payload.id || `browser:${context.actorId}:${payload.fingerprint}`, kind: "browser-onnx", sessionId: context.actorId, expiresAt: this.now() + 30_000, requestId: payload.requestId });
    this.resumeRetryJobs();
    return runtime;
  }

  #submitInference(payload, context) {
    const id = randomUUID();
    const job = this.engine.n.centralJobs.enqueue({ id, idempotencyKey: payload.idempotencyKey || payload.requestId, kind: "inference", payload: { ...payload, actorId: context.actorId }, createdAt: this.now(), requestId: payload.requestId });
    queueMicrotask(() => this.#runInference(job.id).catch(() => {}));
    return job;
  }

  async #discoverHarnessOptions() {
    const codex = await this.harnessProcess.discover();
    const providerModels = this.engine.n.centralProviders.getSnapshot().profiles.flatMap((profile) => [...(profile.models || []).map((model) => model.id), profile.modelId].filter(Boolean));
    const localModels = this.engine.n.centralModelCatalog.getSnapshot().models.filter((model) => model.contextLength >= DEFAULT_CONTEXT_LENGTH).map((model) => model.id);
    return { codex, models: [...new Set([...providerModels, ...localModels])].sort(), profiles: this.engine.n.centralHarnesses.getSnapshot().profiles };
  }

  async #submitExperiment(payload, context) {
    const workspace = this.engine.n.centralWorkspace.getSnapshot().selected;
    if (!workspace) throw new Error("Open a workspace before running the Lab.");
    const harnesses = this.engine.n.centralHarnesses.getSnapshot();
    const harnessId = String(payload.harnessId || harnesses.selectedHarnessId);
    const profile = harnesses.profiles.find((item) => item.id === harnessId);
    if (!profile) throw new Error("Unknown harness profile.");
    const configuration = { ...(harnesses.configurations[harnessId] || {}), ...(payload.configuration || {}) };
    if (profile.stages.includes("act") && !(configuration.validationCommands?.length || configuration.simulatorTarget)) throw new Error("Coding experiments require a validation command or NexusSimulator target.");
    const id = randomUUID();
    const prepared = this.experimentAdapter.prepare({ id, sourceRoot: this.filesystem.root(workspace.id), manifest: { task: payload.task, harnessId, configuration, actorId: context.actorId } });
    const experiment = this.engine.n.centralExperiments.start({ id, task: payload.task, workspaceId: workspace.id, sourceRevision: workspace.revision, baselineHash: prepared.baselineHash, harnessId, configuration, maxAttempts: profile.maxAttempts, startedAt: this.now(), requestId: payload.requestId });
    this.engine.n.centralJobs.enqueue({ id, idempotencyKey: id, kind: "experiment", payload: { experimentId: id }, createdAt: this.now(), requestId: `enqueue:${id}` });
    this.engine.n.centralJobs.claim({ id, route: { id: harnessId, kind: "harness" }, ownerId: this.engine.n.centralNode.getSnapshot().nodeId, expiresAt: this.now() + 30 * 60_000, requestId: `claim:${id}` });
    queueMicrotask(() => this.#runExperiment(id).catch(() => {}));
    return experiment;
  }

  async #runExperiment(id) {
    const run = this.engine.n.centralExperiments.getSnapshot().experiments.find((item) => item.id === id);
    if (!run || run.status !== "running") return;
    const profile = this.engine.n.centralHarnesses.getSnapshot().profiles.find((item) => item.id === run.harnessId);
    const { root, workspace } = this.experimentAdapter.location(id);
    const controller = new AbortController(); this.abortControllers.set(id, controller);
    let review = ""; let previousFailure = ""; let validation = null;
    const runStage = async (stage, attempt, prompt) => {
      const stdoutPath = this.experimentAdapter.artifact(root, "outputs", `${attempt}-${stage}.jsonl`);
      const stderrPath = this.experimentAdapter.artifact(root, "outputs", `${attempt}-${stage}.stderr.log`);
      this.engine.n.centralExperiments.stage({ id, stage, status: "running", attempt, artifact: relativeArtifact(root, stdoutPath), requestId: `stage-start:${id}:${stage}:${attempt}` });
      this.experimentAdapter.event(root, { type: "harness.stage-started", stage, attempt });
      const output = await this.harnessProcess.runCodexStage(profile, stage, run.configuration, prompt, { workspaceRoot: workspace, signal: controller.signal, stdoutPath, stderrPath });
      this.engine.n.centralExperiments.stage({ id, stage, status: "completed", attempt, artifact: relativeArtifact(root, stdoutPath), requestId: `stage-complete:${id}:${stage}:${attempt}` });
      this.experimentAdapter.event(root, { type: "harness.stage-completed", stage, attempt, args: output.args }); this.persist(); this.#emit("experiment.stage-completed", { id, stage, attempt });
      return output.output;
    };
    try {
      if (profile.stages.includes("respond")) await runStage("respond", 1, `Answer this request without changing files: ${run.task}`);
      if (profile.stages.includes("review")) review = await runStage("review", 1, `Review this request without changing files: ${run.task}. Return a concise implementation plan and risks.`);
      if (profile.stages.includes("act")) {
        for (let attempt = 1; attempt <= run.maxAttempts; attempt += 1) {
          await runStage("act", attempt, `Implement this request only inside this disposable SimSpace: ${run.task}. Review context: ${review}. Previous validation failure: ${previousFailure || "none"}. Do not commit, push, deploy, access credentials, or submit externally.`);
          if (profile.stages.includes("audit")) await runStage("audit", attempt, `Audit the SimSpace changes for: ${run.task}. Do not change files. Report correctness and remaining risks.`);
          validation = await this.experimentAdapter.validate(root, workspace, run.configuration.validationCommands, run.configuration.simulatorTarget);
          this.engine.n.centralExperiments.validate({ id, validation, artifacts: ["validation-report.json", ...(validation.simulator?.retained || [])], requestId: `validate:${id}:${attempt}` });
          if (validation.passed) break;
          previousFailure = JSON.stringify(validation);
          if (attempt < run.maxAttempts && validation.repairable) this.engine.n.centralExperiments.retry({ id, requestId: `retry:${id}:${attempt}` });
          else break;
        }
        if (!validation?.passed) throw new Error("Experiment validation failed after the repair budget was exhausted.");
      } else if (profile.stages.includes("audit")) await runStage("audit", 1, `Audit this workspace without changing files for: ${run.task}.`);
      if (!validation) { validation = await this.experimentAdapter.validate(root, workspace, run.configuration.validationCommands, run.configuration.simulatorTarget); this.engine.n.centralExperiments.validate({ id, validation, artifacts: ["validation-report.json", ...(validation.simulator?.retained || [])], requestId: `validate:${id}:final` }); }
      if (!validation.passed) throw new Error("Experiment validation failed.");
      const diff = this.experimentAdapter.diff(root, workspace);
      const completed = this.engine.n.centralExperiments.complete({ id, diff, artifacts: [diff.artifact, "manifest.json", "events.jsonl", "validation-report.json"], finishedAt: this.now(), requestId: `complete:${id}` });
      this.engine.n.centralJobs.finish({ id, status: "completed", result: { experimentId: id, diffHash: diff.hash }, requestId: `finish:${id}` });
      this.engine.n.centralProof.record({ id: `proof:${id}`, jobId: id, assertions: [{ name: "simspace-only", passed: true }, { name: "validation", passed: validation.passed }], artifacts: completed.artifacts, status: validation.passed ? "passed" : "failed", createdAt: this.now(), requestId: `proof:${id}` });
      this.#emit("experiment.completed", { id, diff });
    } catch (error) {
      this.engine.n.centralExperiments.fail({ id, error: error.message, finishedAt: this.now(), requestId: `fail:${id}` });
      this.engine.n.centralJobs.finish({ id, status: controller.signal.aborted ? "cancelled" : "failed", error: error.message, requestId: `finish-failed:${id}` });
      this.experimentAdapter.event(root, { type: "experiment.failed", error: error.message }); this.#emit("experiment.failed", { id, error: error.message });
    } finally { this.abortControllers.delete(id); this.persist(); }
  }

  #approveExperiment(payload, context) {
    const run = this.engine.n.centralExperiments.getSnapshot().experiments.find((item) => item.id === payload.id);
    const workspace = this.engine.n.centralWorkspace.getSnapshot().selected;
    if (!run || !workspace || run.workspaceId !== workspace.id || run.sourceRevision !== workspace.revision) throw new Error("Experiment source revision is no longer current.");
    if (run.diff?.empty) throw new Error("An empty experiment cannot be promoted.");
    return this.engine.n.centralExperiments.approve({ id: run.id, approvedBy: context.actorId, approvedAt: this.now(), sourceRevision: workspace.revision, diffHash: run.diff.hash, requestId: payload.requestId });
  }

  #promoteExperiment(payload, context) {
    const run = this.engine.n.centralExperiments.getSnapshot().experiments.find((item) => item.id === payload.id);
    const workspace = this.engine.n.centralWorkspace.getSnapshot().selected;
    if (!run || !workspace || run.sourceRevision !== workspace.revision) throw new Error("Experiment source revision is no longer current.");
    const leaseId = `promotion:${run.id}`;
    this.engine.n.centralWorkspace.acquireWriter({ jobId: leaseId, holderId: context.actorId, expectedRevision: workspace.revision, expiresAt: this.now() + 60_000, requestId: `lease:${payload.requestId}` });
    try {
      const location = this.experimentAdapter.location(run.id);
      this.experimentAdapter.promote({ sourceRoot: this.filesystem.root(workspace.id), experimentRoot: location.root, expectedBaselineHash: run.baselineHash, expectedDiffHash: run.approval?.diffHash });
      const promoted = this.engine.n.centralExperiments.promote({ id: run.id, diffHash: run.approval?.diffHash, promotedBy: context.actorId, promotedAt: this.now(), sourceRevision: workspace.revision + 1, requestId: payload.requestId });
      this.engine.n.centralWorkspace.releaseWriter({ jobId: leaseId, incrementRevision: true, requestId: `release:${payload.requestId}` }); return promoted;
    } catch (error) { this.engine.n.centralWorkspace.releaseWriter({ jobId: leaseId, incrementRevision: false, requestId: `release-failed:${payload.requestId}` }); throw error; }
  }

  #cancelJob(payload) {
    this.abortControllers.get(payload.id)?.abort();
    return this.engine.n.centralJobs.cancel({ id: payload.id, requestId: payload.requestId });
  }

  resumeRetryJobs() {
    for (const job of this.engine.n.centralJobs.getSnapshot().jobs.filter((item) => item.status === "retry" && item.kind === "inference")) queueMicrotask(() => this.#runInference(job.id).catch(() => {}));
  }

  async #runInference(jobId) {
    const job = this.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === jobId);
    if (!job || !["queued", "retry"].includes(job.status)) return;
    const taskType = String(job.payload.taskType || "text-generation");
    const profiles = this.engine.n.centralProviders.getSnapshot().profiles
      .filter((item) => item.enabled !== false && item.health === "healthy" && taskType === "text-generation" && (!job.payload.providerId || item.id === job.payload.providerId))
      .map((item) => ({ id: item.id, kind: "provider", sourceKind: "provider", providerKind: item.kind, modelId: item.modelId, taskType, contextLength: Number(item.contextLength || DEFAULT_CONTEXT_LENGTH), healthy: true, local: item.local === true, queueDepth: 0 }));
    const runtimes = job.payload.providerId ? [] : this.engine.n.centralModelRuntime.getSnapshot().runtimes
      .filter((item) => item.status === "ready" && item.taskType === taskType && (!item.expiresAt || item.expiresAt > this.now()))
      .map((item) => ({ ...item, sourceKind: "local-runtime", healthy: true, local: item.kind !== "browser-onnx", queueDepth: 0 }));
    const peers = job.payload.allowMesh === false || job.payload.providerId ? [] : this.engine.n.centralMeshMembership.getSnapshot().peers.flatMap((peer) =>
      (peer.capabilities || []).filter((capability) => capability.action === "mesh.inference" && capability.taskType === taskType).map((capability) => ({
        id: `remote:${peer.transport}:${peer.nodeId}:${capability.providerId}`, kind: "remote-peer", sourceKind: "remote-peer",
        nodeId: peer.nodeId, transport: peer.transport, transportPeerId: peer.transportPeerId, providerId: capability.providerId,
        modelId: capability.modelId, taskType, contextLength: capability.contextLength, healthy: true, local: false,
        queueDepth: Number(capability.queueDepth || 0), concurrencyLimit: Number(capability.concurrencyLimit || 1)
      })));
    let decision;
    try {
      decision = this.engine.n.centralRouting.select({ requestId: `route:${job.id}:${this.now()}`, contextLength: DEFAULT_CONTEXT_LENGTH, candidates: [...profiles, ...runtimes, ...peers] });
    } catch (error) {
      this.engine.n.centralJobs.retry({ id: job.id, error: error.message, requestId: `retry:${job.id}:${this.now()}` }); this.persist(); this.#emit("job.retry", { jobId, error: error.message }); return;
    }
    this.engine.n.centralJobs.claim({ id: job.id, route: decision.selected, ownerId: this.engine.n.centralNode.getSnapshot().nodeId, expiresAt: this.now() + 10 * 60_000, requestId: `claim:${job.id}:${this.now()}` });
    this.engine.n.centralInference.begin({ jobId, modelId: decision.selected.modelId, contextLength: DEFAULT_CONTEXT_LENGTH, requestId: `stream:${job.id}:${this.now()}` });
    const controller = new AbortController(); this.abortControllers.set(jobId, controller);
    let sequence = 0;
    const onChunk = async (content) => {
      this.engine.n.centralInference.push({ jobId, sequence, content, requestId: `chunk:${jobId}:${sequence}` });
      this.engine.n.centralJobs.progress({ id: jobId, sequence, content, at: this.now(), requestId: `progress:${jobId}:${sequence}` });
      this.#emit("inference.chunk", { jobId, sequence, content }); sequence += 1;
    };
    let selectedRoute = decision.selected;
    try {
      let lastError = null;
      for (const candidate of [decision.selected, ...decision.fallbacks]) {
        selectedRoute = candidate;
        if (candidate.id !== decision.selected.id) this.engine.n.centralJobs.reroute({ id: jobId, route: candidate, requestId: `reroute:${jobId}:${candidate.id}` });
        try {
          if (candidate.kind === "browser-onnx") {
            if (!this.browserDispatcher) throw new Error("No browser inference dispatcher is connected.");
            await this.browserDispatcher(candidate.sessionId, { jobId, request: job.payload }, onChunk, controller.signal);
          } else if (candidate.kind === "remote-peer") {
            const remote = await this.mesh.request(candidate, "mesh.inference", { ...job.payload, remoteProviderId: candidate.providerId }, { signal: controller.signal, onChunk });
            if (!remote.streamed) for (const chunk of remote.chunks || []) await onChunk(chunk.content);
          } else {
            const profile = this.engine.n.centralProviders.getSnapshot().profiles.find((item) => item.id === candidate.id);
            await this.providers.streamChat(profile, job.payload, onChunk, controller.signal);
          }
          lastError = null;
          break;
        } catch (error) {
          lastError = error;
          if (sequence > 0 || controller.signal.aborted) throw error;
        }
      }
      if (lastError) throw lastError;
      this.engine.n.centralInference.finish({ jobId, status: "completed", requestId: `stream-finish:${jobId}:${this.now()}` });
      this.engine.n.centralJobs.finish({ id: jobId, status: "completed", result: { chunks: sequence, route: selectedRoute.id }, requestId: `finish:${jobId}` });
      this.#emit("job.completed", { jobId, route: selectedRoute.id });
    } catch (error) {
      const status = controller.signal.aborted ? "cancelled" : sequence > 0 ? "failed" : "retry";
      this.engine.n.centralInference.finish({ jobId, status, requestId: `stream-finish:${jobId}:${this.now()}` });
      if (status === "retry") this.engine.n.centralJobs.retry({ id: jobId, error: error.message, requestId: `retry:${jobId}:${this.now()}` });
      else this.engine.n.centralJobs.finish({ id: jobId, status, error: error.message, requestId: `finish:${jobId}` });
      this.#emit(`job.${status}`, { jobId, error: error.message });
    } finally { this.abortControllers.delete(jobId); this.persist(); }
  }

  async #joinMesh(payload) {
    return this.#connectMesh({ ...payload, transport: "lan", code: payload.lanCode });
  }

  meshCapabilities() {
    const providers = this.engine.n.centralProviders.getSnapshot().profiles.filter((item) => item.enabled !== false && item.health === "healthy" && item.modelId && item.sharePolicy?.enabled);
    return { nodeId: this.engine.n.centralNode.getSnapshot().nodeId, capabilities: [{ action: "mesh.ping" }, { action: "mesh.capabilities" }, ...providers.map((profile) => ({ action: "mesh.inference", taskType: "text-generation", providerId: profile.id, providerKind: profile.kind, modelId: profile.modelId, contextLength: profile.contextLength, concurrencyLimit: profile.sharePolicy.concurrencyLimit }))] };
  }

  async #connectMesh(payload) {
    const transport = String(payload.transport || "lan"); const code = String(payload.code || payload.lanCode || payload.roomCode || "");
    if (!code) throw new Error("Mesh code is required.");
    const status = transport === "peerjs"
      ? await this.mesh.start({ transport, roomCode: code, brokerUrl: payload.brokerUrl, iceServers: payload.iceServers || [] })
      : await this.mesh.start({ transport, lanCode: code });
    const membership = this.mesh.deriveMembership(code, transport);
    this.engine.n.centralMeshMembership.join({ transport, networkId: membership.networkId, connectedAt: this.now(), requestId: payload.requestId });
    return status;
  }

  async #disconnectMesh(payload) {
    const transport = String(payload.transport || "lan"); await this.mesh.stop(transport);
    return this.engine.n.centralMeshMembership.leave({ transport, requestId: payload.requestId });
  }

  async #observeMeshPeer(peer) {
    const transportPeerId = peer.transportPeerId || peer.peerId || peer.nodeId;
    const existing = this.engine.n.centralMeshMembership.getSnapshot().peers.find((item) => item.transport === peer.transport && item.transportPeerId === transportPeerId);
    let nodeId = existing?.nodeId || transportPeerId; let capabilities = existing?.capabilities || [];
    if (!capabilities.length) {
      try { const response = await this.mesh.request({ transport: peer.transport, transportPeerId, nodeId }, "mesh.capabilities", {}); nodeId = response.nodeId || nodeId; capabilities = response.capabilities || response || []; } catch (_error) {}
    }
    this.engine.n.centralMeshMembership.observePeer({ nodeId, transport: peer.transport, transportPeerId, addresses: peer.addresses, capabilities, expiresAt: this.now() + 30_000, requestId: `peer:${peer.transport}:${transportPeerId}:${this.now()}` });
    this.persist(); if (!existing) this.#emit("mesh.peer-observed", { nodeId, transport: peer.transport });
  }

  async #handleMeshRequest(request, peer, stream = {}) {
    if (request.expiresAt <= this.now()) throw new Error("Mesh request expired.");
    if (request.action === "mesh.ping") return { pong: true, nodeId: this.engine.n.centralNode.getSnapshot().nodeId, at: this.now() };
    if (request.action === "mesh.capabilities") return this.meshCapabilities();
    const decision = this.engine.n.centralPolicy.evaluate({ peerId: peer.peerId, action: request.action, requestId: request.requestId });
    if (decision.decision !== "allow") throw new Error("Local policy denied this mesh action.");
    if (request.action !== "mesh.inference") throw new Error("Unsupported mesh action.");
    const profile = this.engine.n.centralProviders.getSnapshot().profiles.find((item) => item.id === request.payload.remoteProviderId && item.health === "healthy" && item.sharePolicy?.enabled);
    if (!profile) throw new Error("Requested provider is not shared by this host.");
    const release = this.#admitRemote(profile, peer.peerId, Number(request.payload.maxOutputTokens || DEFAULT_CONTEXT_LENGTH));
    try {
      const job = this.#submitInference({ ...request.payload, providerId: profile.id, requestId: request.requestId, idempotencyKey: request.requestId, allowMesh: false }, { actorId: peer.peerId, scopes: ["view"] });
      const finished = await this.#waitForJob(job.id, request.expiresAt, stream.onChunk, stream.signal);
      if (finished.status !== "completed") throw new Error(finished.error || `Remote inference ${finished.status}.`);
      return { jobId: finished.id, chunks: stream.onChunk ? [] : finished.progress, streamed: Boolean(stream.onChunk), result: finished.result };
    } finally { release(); }
  }

  #admitRemote(profile, peerId, requestedTokens) {
    const key = `${peerId}:${profile.id}`; const now = this.now(); const previous = this.remoteUsage.get(key) || { windowStartedAt: now, tokens: 0, active: 0 };
    const usage = now - previous.windowStartedAt >= 60_000 ? { windowStartedAt: now, tokens: 0, active: previous.active } : previous;
    if (usage.active >= profile.sharePolicy.concurrencyLimit) throw new Error("Shared provider concurrency limit reached.");
    const tokens = Math.max(1, Math.min(DEFAULT_CONTEXT_LENGTH, requestedTokens));
    if (usage.tokens + tokens > profile.sharePolicy.tokensPerMinute) throw new Error("Shared provider token budget reached.");
    usage.active += 1; usage.tokens += tokens; this.remoteUsage.set(key, usage);
    return () => { const current = this.remoteUsage.get(key); if (current) current.active = Math.max(0, current.active - 1); };
  }

  async #waitForJob(jobId, deadline, onChunk, signal) {
    let delivered = 0;
    while (this.now() < deadline) {
      const job = this.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === jobId);
      if (signal?.aborted) { this.abortControllers.get(jobId)?.abort(); throw new Error("Remote inference cancelled."); }
      while (onChunk && delivered < (job?.progress?.length || 0)) await onChunk(job.progress[delivered++].content);
      if (job && ["completed", "failed", "cancelled"].includes(job.status)) return job;
      await new Promise((resolve) => setTimeout(resolve, 50));
    }
    throw new Error("Mesh inference deadline expired.");
  }

  setBrowserDispatcher(dispatcher) { this.browserDispatcher = dispatcher; }
  getSession(sessionId) { return this.engine.n.centralLanAccess.getSnapshot().sessions.find((item) => item.id === sessionId && !item.revoked && item.expiresAt > this.now()) || null; }
  #emit(type, data) { const event = { id: randomUUID(), type, data, at: this.now() }; this.store.appendEvent(event); this.emit("event", event); }
  async close() { await this.mesh.stop(); this.persist(); this.store.close(); }
}

function relativeArtifact(root, path) { return relative(root, path); }
