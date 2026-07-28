import { commandEnvelope } from "@nexuscentral/contracts";

export class CentralClient {
  constructor(transport) {
    this.transport = transport;
  }

  command(command, payload, options) {
    return this.transport.command(commandEnvelope(command, payload, options));
  }

  getCapabilities() { return this.transport.getCapabilities(); }
  chooseWorkspace() { return this.transport.chooseWorkspace?.() || Promise.resolve(null); }
  getState() { return this.command("system.state", {}); }
  openWorkspace(payload) { return this.command("workspace.open", payload); }
  listWorkspace() { return this.command("workspace.list", {}); }
  listProviders() { return this.command("providers.list", {}); }
  saveProvider(payload) { return this.command("providers.save", payload); }
  testProvider(id) { return this.command("providers.test", { id }); }
  setProviderSharePolicy(payload) { return this.command("providers.set-share-policy", payload); }
  listModels() { return this.command("models.list", {}); }
  listInstalledModels() { return this.command("models.list-installed", {}); }
  searchModels(query) { return this.command("models.search", { query }); }
  downloadModel(modelId) { return this.command("models.download", { modelId }); }
  loadModel(modelId) { return this.command("models.load", { modelId }); }
  unloadModel(instanceId) { return this.command("models.unload", { instanceId }); }
  importHarness(payload) { return this.command("harnesses.import", payload); }
  selectHarness(id) { return this.command("harnesses.select", { id }); }
  configureHarness(payload) { return this.command("harnesses.configure", payload); }
  discoverHarnessOptions() { return this.command("harnesses.discover", {}); }
  listHarnesses() { return this.command("harnesses.list", {}); }
  submitInference(payload) { return this.command("inference.submit", payload); }
  submitRun(payload) { return this.command("runs.submit", payload); }
  getRun(id) { return this.command("runs.get", { id }); }
  cancelRun(id) { return this.command("runs.cancel", { id }); }
  cancelJob(id) { return this.command("jobs.cancel", { id }); }
  submitLabRun(payload) { return this.command("lab.submit", payload); }
  approveExperiment(id) { return this.command("experiments.approve", { id }); }
  promoteExperiment(id) { return this.command("experiments.promote", { id }); }
  listArtifacts(runId = "") { return this.command("artifacts.list", runId ? { runId } : {}); }
  readArtifact(id) { return this.command("artifacts.read", { id }); }
  serviceStatus() { return this.command("service.status", {}); }
  installService() { return this.command("service.install", {}); }
  uninstallService() { return this.command("service.uninstall", {}); }
  createPairing() { return this.command("lan.create-pairing", {}); }
  grantSessionScope(payload) { return this.command("lan.grant-scope", payload); }
  revokeSession(sessionId) { return this.command("lan.revoke", { sessionId }); }
  joinMesh(lanCode) { return this.command("mesh.join", { lanCode }); }
  connectMesh(payload) { return this.command("mesh.connect", payload); }
  disconnectMesh(transport) { return this.command("mesh.disconnect", { transport }); }
  setMeshSharePolicy(payload) { return this.command("mesh.set-share-policy", payload); }
  meshState() { return this.command("mesh.state", {}); }
  grantPeerInference(peerId, grantId) { return this.command("policy.grant", { peerId, action: "mesh.inference", grantId }); }
  revokePeerGrant(grantId) { return this.command("policy.revoke", { grantId }); }
  subscribe(listener) { return this.transport.subscribe(listener); }
}

export class DirectTransport {
  constructor(node, context = { actorId: "local", scopes: ["admin"] }) { this.node = node; this.context = context; }
  getCapabilities() { return Promise.resolve(this.node.capabilities()); }
  command(envelope) { return this.node.execute(envelope, this.context); }
  subscribe(listener) { this.node.on("event", listener); return () => this.node.off("event", listener); }
}

export class HttpTransport {
  constructor(baseUrl, fetchImpl = (...args) => globalThis.fetch(...args)) { this.baseUrl = baseUrl.replace(/\/$/, ""); this.fetchImpl = fetchImpl; }
  async getCapabilities() { return this.#json(`${this.baseUrl}/api/v1/capabilities`); }
  async command(envelope) { return this.#json(`${this.baseUrl}/api/v1/commands`, { method: "POST", headers: { "Content-Type": "application/json" }, credentials: "include", body: JSON.stringify(envelope) }); }
  subscribe(listener) {
    const socket = new WebSocket(this.baseUrl.replace(/^http/, "ws") + "/api/v1/events");
    socket.addEventListener("message", (event) => listener(JSON.parse(event.data)));
    return () => socket.close();
  }
  async #json(url, options) {
    const response = await this.fetchImpl(url, { credentials: "include", ...options });
    const body = await response.json();
    if (!response.ok || body.ok === false) throw new Error(body.error?.message || `HTTP ${response.status}`);
    return body.data ?? body;
  }
}

export class ElectronIpcTransport {
  constructor(bridge) { this.bridge = bridge; }
  getCapabilities() { return this.bridge.getCapabilities(); }
  command(envelope) { return this.bridge.command(envelope); }
  chooseWorkspace() { return this.bridge.chooseWorkspace(); }
  subscribe(listener) { return this.bridge.subscribe(listener); }
}
