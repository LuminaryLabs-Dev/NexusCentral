import { installIdentity } from './domain-kits/identity-kit.mjs';
import { installParticleField } from './domain-kits/particle-field-kit.mjs';

const bridge = window.nexusCentral || {
  getStatus: async () => ({
    app: 'NexusCentral Web preview',
    workflow: 'Lab / Setup / Review / Settings',
    capabilities: [],
    lmStudioBaseUrl: 'desktop only',
    harnessRoot: 'desktop only',
    repoRoot: 'browser'
  }),
  chooseWorkspace: async () => ({ ok: false, message: 'Desktop helper unavailable.', entries: [] }),
  refreshWorkspace: async () => ({ ok: false, message: 'Desktop helper unavailable.', entries: [] }),
  createSandbox: async () => ({ ok: false, message: 'Desktop helper unavailable.', entries: [] }),
  runRustSidecar: async () => ({ ok: false, stdout: '', stderr: 'Desktop helper unavailable.' }),
  runLabLoop: async () => ({ ok: false, message: 'Desktop helper unavailable.' }),
  getLabState: async () => ({ ok: false, message: 'No Lab run has been completed yet.' }),
  getRouterState: async () => ({ state: 'inactive', endpointLabel: 'None', message: 'Desktop helper unavailable.', activeProvider: {}, providers: [], templates: [], models: [] }),
  saveAndTestRouter: async () => ({ state: 'unhealthy', endpointLabel: 'None', message: 'Desktop helper unavailable.', activeProvider: {}, providers: [], templates: [], models: [] }),
  listModels: async () => ({ ok: false, models: [], message: 'Desktop helper unavailable.' }),
  searchModels: async () => ({ ok: false, models: [], message: 'Desktop helper unavailable.' }),
  downloadModel: async () => ({ ok: false, message: 'Desktop helper unavailable.' }),
  loadModel: async () => ({ ok: false, message: 'Desktop helper unavailable.' }),
  unloadModel: async () => ({ ok: false, message: 'Desktop helper unavailable.' }),
  publishModelProvider: async () => ({ state: 'unhealthy', message: 'Desktop helper unavailable.' }),
  getMeshState: async () => ({ running: false, enabled: false, nodeId: '', nodeName: '', peers: [] }),
  configureMesh: async () => ({ running: false, peers: [] }),
  disableMesh: async () => ({ running: false, peers: [] }),
  pingMeshPeer: async () => ({ ok: false, message: 'Desktop helper unavailable.' })
};

const state = {
  activeSurface: 'lab',
  loopRunning: false,
  routeReady: false,
  providerTemplates: []
};

function setText(id, value) {
  const element = document.getElementById(id);
  if (element) {
    element.textContent = value;
  }
}

function selectSurface(id) {
  state.activeSurface = id;

  document.querySelectorAll('[data-surface]').forEach((surface) => {
    surface.hidden = surface.dataset.surface !== id;
  });
  document.querySelectorAll('[data-nav]').forEach((button) => {
    button.classList.toggle('active', button.dataset.nav === id);
  });

}

function renderWorkspace(entries) {
  const list = document.getElementById('workspace-list');
  list.innerHTML = '';

  for (const entry of entries.slice(0, 24)) {
    const item = document.createElement('li');
    item.textContent = entry.name;
    item.className = entry.kind === 'folder' ? 'folder-entry' : 'file-entry';
    list.appendChild(item);
  }
}

function updateWorkspaceSummary(result) {
  setText('workspace-result', result.message);
  setText('workspace-path', result.ok && result.path ? `Path: ${result.path}` : 'Path: none');
  setText('workspace-count', `${(result.entries || []).length} item${(result.entries || []).length === 1 ? '' : 's'}`);
  renderWorkspace(result.entries || []);
}

function renderMeshState(mesh) {
  const peers = mesh.peers || [];
  setText('mesh-state', mesh.running ? `Connected / ${peers.length} peer${peers.length === 1 ? '' : 's'}` : 'Not connected');
  setText('mesh-node-id', mesh.nodeId ? `This computer: ${mesh.nodeId}` : '');
  const nameInput = document.getElementById('mesh-node-name');
  if (document.activeElement !== nameInput) nameInput.value = mesh.nodeName || '';
  document.getElementById('mesh-lan-code').value = '';

  const container = document.getElementById('mesh-peers');
  container.innerHTML = '';
  container.classList.toggle('empty-list', peers.length === 0);
  if (peers.length === 0) {
    container.textContent = mesh.running ? 'Waiting for computers using this LAN code.' : 'Join a mesh to discover computers.';
    return;
  }

  for (const peer of peers) {
    const row = document.createElement('div');
    row.className = 'mesh-peer-row';
    const identity = document.createElement('div');
    const name = document.createElement('strong');
    name.textContent = peer.nodeName || peer.nodeId;
    const address = document.createElement('span');
    address.textContent = `${peer.address} / ${peer.nodeId}`;
    identity.append(name, address);
    const ping = document.createElement('button');
    ping.type = 'button';
    ping.textContent = 'Ping';
    ping.dataset.meshPing = peer.nodeId;
    row.append(identity, ping);
    container.appendChild(row);
  }
}

async function loadMeshState() {
  renderMeshState(await bridge.getMeshState());
}

async function configureMesh(event) {
  event.preventDefault();
  setText('mesh-state', 'Joining...');
  try {
    renderMeshState(await bridge.configureMesh({
      nodeName: document.getElementById('mesh-node-name').value,
      lanCode: document.getElementById('mesh-lan-code').value
    }));
  } catch (error) {
    setText('mesh-state', error.message || 'Could not join the mesh.');
  }
}

async function disableMesh() {
  renderMeshState(await bridge.disableMesh());
}

async function pingMeshPeer(event) {
  const button = event.target.closest('[data-mesh-ping]');
  if (!button) return;
  button.disabled = true;
  const result = await bridge.pingMeshPeer(button.dataset.meshPing);
  button.textContent = result.ok ? 'Online' : 'Failed';
  button.disabled = false;
}

function activeProviderTemplate() {
  const providerType = document.getElementById('router-provider-type').value;
  return state.providerTemplates.find((item) => item.id === providerType) || {};
}

function renderRouterModelOptions(models = []) {
  const list = document.getElementById('router-model-options');
  list.innerHTML = '';
  for (const model of models) {
    const option = document.createElement('option');
    option.value = model.id;
    option.label = model.name || model.id;
    list.appendChild(option);
  }
}

function applyProviderTemplate() {
  const template = activeProviderTemplate();
  document.getElementById('router-base-url').value = template.baseUrl || '';
  document.getElementById('router-api-key').value = '';
  document.getElementById('router-api-key').placeholder = template.apiKeyPlaceholder || 'Optional bearer token';
  setText('router-key-status', template.apiKeyRequired ? 'required' : 'optional');
}

function renderRouterState(route) {
  state.routeReady = route.state === 'available';
  setText('router-state', state.routeReady ? 'Available' : 'Unavailable');
  setText('router-result', route.message || 'Route status unavailable.');
}

async function loadRouterState() {
  const route = await bridge.getRouterState();
  state.providerTemplates = route.templates || [];
  const provider = route.activeProvider || {};
  document.getElementById('router-provider-type').value = provider.providerType || 'lm-studio';
  document.getElementById('router-base-url').value = provider.baseUrl || activeProviderTemplate().baseUrl || '';
  document.getElementById('router-api-key').value = '';
  document.getElementById('router-model-id').value = provider.modelId || '';
  const template = activeProviderTemplate();
  document.getElementById('router-api-key').placeholder = provider.hasApiKey
    ? 'Saved securely; leave blank to keep'
    : (template.apiKeyPlaceholder || 'Optional bearer token');
  setText('router-key-status', provider.hasApiKey ? 'saved securely' : (template.apiKeyRequired ? 'required' : 'optional'));
  renderRouterModelOptions(route.models || []);
  renderRouterState(route);
}

async function saveAndTestRouter(event) {
  event.preventDefault();
  setText('router-state', 'Testing');
  setText('router-result', 'Testing provider...');
  const template = activeProviderTemplate();
  const route = await bridge.saveAndTestRouter({
    id: document.getElementById('router-provider-type').value,
    providerType: document.getElementById('router-provider-type').value,
    name: template.name,
    baseUrl: document.getElementById('router-base-url').value,
    apiKey: document.getElementById('router-api-key').value,
    modelId: document.getElementById('router-model-id').value
  });
  renderRouterModelOptions(route.models || []);
  if (!document.getElementById('router-model-id').value && route.activeProvider?.modelId) {
    document.getElementById('router-model-id').value = route.activeProvider.modelId;
  }
  renderRouterState(route);
}

function modelActionButton(label, action, modelId) {
  const button = document.createElement('button');
  button.type = 'button';
  button.textContent = label;
  button.dataset.modelAction = action;
  button.dataset.modelId = modelId;
  return button;
}

function renderModelRows(containerId, models, kind) {
  const container = document.getElementById(containerId);
  container.innerHTML = '';
  container.classList.toggle('empty-list', models.length === 0);
  if (models.length === 0) {
    container.textContent = kind === 'search' ? 'No matching models.' : 'No local models found.';
    return;
  }
  for (const model of models) {
    const row = document.createElement('article');
    row.className = 'model-row';
    const copy = document.createElement('div');
    const title = document.createElement('strong');
    title.textContent = model.name || model.id;
    const meta = document.createElement('span');
    meta.textContent = kind === 'search'
      ? `${model.downloads.toLocaleString()} downloads`
      : `${model.type} / ${model.state}`;
    copy.append(title, meta);
    const actions = document.createElement('div');
    actions.className = 'model-actions';
    if (kind === 'search') {
      actions.append(modelActionButton('Download', 'download', model.id));
    } else {
      const loaded = Boolean(model.instanceId) || (model.state.includes('loaded') && !model.state.includes('not'));
      actions.append(modelActionButton(loaded ? 'Unload' : 'Load', loaded ? 'unload' : 'load', loaded ? (model.instanceId || model.id) : model.id));
      actions.append(modelActionButton('Use as provider', 'publish', model.id));
    }
    row.append(copy, actions);
    container.appendChild(row);
  }
}

async function refreshModels() {
  setText('models-status', 'Refreshing local models...');
  const result = await bridge.listModels();
  setText('models-status', result.message);
  renderModelRows('installed-models', result.models || [], 'installed');
}

async function searchModels(event) {
  event.preventDefault();
  setText('models-status', 'Searching downloadable models...');
  const result = await bridge.searchModels(document.getElementById('model-search-query').value);
  setText('models-status', result.message);
  renderModelRows('model-search-results', result.models || [], 'search');
}

async function handleModelAction(event) {
  const button = event.target.closest('[data-model-action]');
  if (!button) return;
  button.disabled = true;
  const action = button.dataset.modelAction;
  const modelId = button.dataset.modelId;
  const result = action === 'download'
    ? await bridge.downloadModel(modelId)
    : action === 'load'
      ? await bridge.loadModel(modelId)
      : action === 'unload'
        ? await bridge.unloadModel(modelId)
        : await bridge.publishModelProvider(modelId);
  setText('models-status', result.message || 'Model action completed.');
  if (action === 'publish') {
    renderRouterState(result);
  } else if (action !== 'download') {
    await refreshModels();
  }
  button.disabled = false;
}

function appendTranscript(label, text) {
  const transcript = document.getElementById('transcript');
  const card = document.createElement('article');
  card.className = 'summary-card';

  const small = document.createElement('span');
  small.textContent = label;
  const strong = document.createElement('strong');
  strong.textContent = text;

  card.append(small, strong);
  transcript.appendChild(card);
  transcript.scrollTop = transcript.scrollHeight;
}

function updateLoopReview(result) {
  const stages = new Map((result.stages || []).map((stage) => [stage.stage, stage]));
  for (const stage of ['review', 'act', 'audit']) {
    const item = stages.get(stage);
    const status = item ? (item.ok ? 'complete' : 'failed') : 'pending';
    setText(`${stage}-stage-status`, `${stage[0].toUpperCase()}${stage.slice(1)}: ${status}`);
  }
  setText('latest-loop-result', result.ok ? `${result.message} ${result.finalMessage || ''}` : (result.message || result.finalMessage || 'Lab run failed.'));
}

function updateLoopState(result) {
  const loopState = document.getElementById('loop-state');
  loopState.hidden = false;
  if (result?.ok) {
    setText('loop-state', result.message || 'Lab loop completed.');
  } else {
    setText('loop-state', result?.message || result?.finalMessage || 'Lab loop needs attention.');
  }
  if (result) {
    updateLoopReview(result);
  }
}

async function loadStatus() {
  const status = await bridge.getStatus();
  setText('status-root', status.repoRoot);
  const labState = await bridge.getLabState();
  if (labState.ok) {
    updateLoopState(labState);
  }
  await loadRouterState();
  await loadMeshState();
}

async function openWorkspace() {
  const result = await bridge.chooseWorkspace();
  updateWorkspaceSummary(result.ok ? { ...result, message: 'Workspace opened.' } : result);
}

async function refreshWorkspace() {
  const result = await bridge.refreshWorkspace();
  updateWorkspaceSummary(result);
}

async function createSandbox() {
  const result = await bridge.createSandbox();
  updateWorkspaceSummary(result);
}

async function sendMessage() {
  const input = document.getElementById('prompt-input');
  const text = input.value.trim();
  if (!text) {
    input.focus();
    return;
  }

  appendTranscript('You', text);
  if (state.loopRunning) {
    return;
  }

  state.loopRunning = true;
  document.getElementById('loop-state').hidden = false;
  setText('loop-state', 'Reviewing the workspace...');
  const result = await bridge.runLabLoop({ task: text });
  state.loopRunning = false;
  updateLoopState(result);
  appendTranscript('Lab', result.ok ? result.finalMessage : (result.message || result.finalMessage));
  input.value = '';
  input.focus();
}

async function runSidecar() {
  const result = await bridge.runRustSidecar();
  setText('sidecar-result', result.ok ? 'Native helper responded.' : (result.stderr || 'Native helper failed.'));
}

installIdentity();
installParticleField(document.getElementById('particle-field'));

document.querySelectorAll('[data-nav]').forEach((button) => {
  button.addEventListener('click', () => {
    selectSurface(button.dataset.nav);
    if (button.dataset.nav === 'models') refreshModels();
    if (button.dataset.nav === 'settings') loadMeshState();
  });
});

document.getElementById('open-workspace').addEventListener('click', openWorkspace);
document.getElementById('refresh-workspace').addEventListener('click', refreshWorkspace);
document.getElementById('create-sandbox').addEventListener('click', createSandbox);
document.getElementById('attach-context').addEventListener('click', openWorkspace);
document.getElementById('send-message').addEventListener('click', sendMessage);
document.getElementById('prompt-input').addEventListener('keydown', (event) => {
  if (event.key === 'Enter' && !event.shiftKey && !event.isComposing) {
    event.preventDefault();
    sendMessage();
  }
});
document.getElementById('run-sidecar').addEventListener('click', runSidecar);
document.getElementById('router-provider-type').addEventListener('change', applyProviderTemplate);
document.getElementById('router-form').addEventListener('submit', saveAndTestRouter);
document.getElementById('refresh-models').addEventListener('click', refreshModels);
document.getElementById('model-search-form').addEventListener('submit', searchModels);
document.getElementById('model-search-results').addEventListener('click', handleModelAction);
document.getElementById('installed-models').addEventListener('click', handleModelAction);
document.getElementById('mesh-form').addEventListener('submit', configureMesh);
document.getElementById('mesh-disconnect').addEventListener('click', disableMesh);
document.getElementById('mesh-refresh').addEventListener('click', loadMeshState);
document.getElementById('mesh-peers').addEventListener('click', pingMeshPeer);

selectSurface('lab');
loadStatus();
