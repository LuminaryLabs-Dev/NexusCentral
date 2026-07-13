const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawn } = require('child_process');
const { app, BrowserWindow, ipcMain, dialog, safeStorage } = require('electron');
const { workflowSurfaces, desktopCapabilities } = require('./workflow-contract');
const { RouterService } = require('./services/router-service');
const { ModelsService } = require('./services/models-service');
const { MeshRoutingService } = require('./services/mesh-routing-service');

const repoRoot = path.resolve(__dirname, '..', '..');
const defaultLmStudioBaseUrl = process.env.NEXUSCENTRAL_LM_STUDIO_BASE_URL || 'http://10.0.0.38:1234';
let mainWindow;
let selectedWorkspacePath = '';
let routerService;
let modelsService;
let meshRoutingService;

function createWindow() {
  mainWindow = new BrowserWindow({
    width: 1360,
    height: 900,
    minWidth: 1040,
    minHeight: 720,
    title: 'NexusCentral',
    backgroundColor: '#081d19',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: false
    }
  });

  mainWindow.on('closed', () => {
    mainWindow = undefined;
  });
  mainWindow.webContents.on('did-fail-load', (_event, errorCode, errorDescription) => {
    console.error(`window:did-fail-load ${errorCode} ${errorDescription}`);
  });
  mainWindow.removeMenu();
  mainWindow.loadFile(path.join(__dirname, 'index.html'))
    .then(() => {
      mainWindow?.show();
      mainWindow?.focus();
    })
    .catch((error) => {
      console.error(`window:loadFile:failed ${error.message}`);
    });
}

function sidecarPath() {
  const binaryName = process.platform === 'win32' ? 'nexuscentral-rust-proof.exe' : 'nexuscentral-rust-proof';
  return path.join(__dirname, '..', 'sidecars', 'rust-proof', 'target', 'debug', binaryName);
}

function runCommand(command, args, options = {}) {
  return new Promise((resolve) => {
    const child = spawn(command, args, {
      cwd: options.cwd || repoRoot,
      env: { ...process.env, ...(options.env || {}) },
      timeout: options.timeoutMs || 15000,
      shell: options.shell === true
    });
    let stdout = '';
    let stderr = '';
    child.stdout.on('data', (chunk) => {
      stdout += chunk.toString();
    });
    child.stderr.on('data', (chunk) => {
      stderr += chunk.toString();
    });
    child.on('error', (error) => {
      resolve({ ok: false, stdout, stderr: error.message, exitCode: -1 });
    });
    child.on('close', (code) => {
      resolve({ ok: code === 0, stdout, stderr, exitCode: code ?? -1 });
    });
    if (options.input !== undefined) {
      child.stdin.end(String(options.input));
    }
  });
}

function readWorkspaceEntries(workspacePath) {
  if (!workspacePath || !fs.existsSync(workspacePath)) {
    return [];
  }

  return fs.readdirSync(workspacePath, { withFileTypes: true })
    .slice(0, 80)
    .map((entry) => ({
      name: entry.name,
      kind: entry.isDirectory() ? 'folder' : 'file'
    }));
}

function codexExecutable() {
  const candidates = [
    process.env.NEXUSCENTRAL_CODEX_PATH,
    path.join(os.homedir(), '.local', 'bin', 'codex'),
    '/opt/homebrew/bin/codex',
    '/usr/local/bin/codex',
    'codex'
  ].filter(Boolean);

  for (const candidate of candidates) {
    if (candidate === 'codex') {
      return candidate;
    }
    try {
      if (fs.statSync(candidate).isFile() && (process.platform === 'win32' || (fs.statSync(candidate).mode & 0o111) !== 0)) {
        return candidate;
      }
    } catch (_error) {
      // Continue to the next known install location.
    }
  }

  return 'codex';
}

function getRouterService() {
  if (!routerService) {
    routerService = new RouterService({
      registryPath: path.join(app.getPath('userData'), 'router', 'registry.json'),
      secretStore: getProviderSecretStore()
    });
  }
  return routerService;
}

function providerSecretsPath() {
  return path.join(app.getPath('userData'), 'router', 'secrets.json');
}

function readProviderSecrets() {
  const source = providerSecretsPath();
  if (!fs.existsSync(source)) {
    return {};
  }
  try {
    return JSON.parse(fs.readFileSync(source, 'utf8'));
  } catch (_error) {
    return {};
  }
}

function getProviderSecretStore() {
  return {
    set(id, value) {
      if (!safeStorage.isEncryptionAvailable()) {
        throw new Error('Secure credential storage is unavailable on this system.');
      }
      const secrets = readProviderSecrets();
      secrets[id] = safeStorage.encryptString(value).toString('base64');
      fs.mkdirSync(path.dirname(providerSecretsPath()), { recursive: true });
      fs.writeFileSync(providerSecretsPath(), JSON.stringify(secrets, null, 2), 'utf8');
    },
    get(id) {
      const encoded = readProviderSecrets()[id];
      if (!encoded || !safeStorage.isEncryptionAvailable()) {
        return '';
      }
      try {
        return safeStorage.decryptString(Buffer.from(encoded, 'base64'));
      } catch (_error) {
        return '';
      }
    },
    has(id) {
      return Boolean(readProviderSecrets()[id]);
    }
  };
}

function getModelsService() {
  if (!modelsService) {
    modelsService = new ModelsService({
      getLmStudioConnection() {
        const state = getRouterService().currentState();
        const profile = state.providers.find((item) => item.providerType === 'lm-studio');
        return {
          baseUrl: profile?.baseUrl || `${defaultLmStudioBaseUrl.replace(/\/+$/, '')}/v1`,
          apiKey: profile ? getProviderSecretStore().get(profile.id) : ''
        };
      }
    });
  }
  return modelsService;
}

function meshSecretsPath() {
  return path.join(app.getPath('userData'), 'mesh', 'secrets.json');
}

function readMeshSecrets() {
  return readJsonFile(meshSecretsPath());
}

function readJsonFile(source) {
  if (!fs.existsSync(source)) return {};
  try {
    return JSON.parse(fs.readFileSync(source, 'utf8'));
  } catch (_error) {
    return {};
  }
}

function getMeshSecretStore() {
  return {
    set(id, value) {
      if (!safeStorage.isEncryptionAvailable()) {
        throw new Error('Secure LAN code storage is unavailable on this system.');
      }
      const secrets = readMeshSecrets();
      secrets[id] = safeStorage.encryptString(value).toString('base64');
      fs.mkdirSync(path.dirname(meshSecretsPath()), { recursive: true });
      fs.writeFileSync(meshSecretsPath(), JSON.stringify(secrets, null, 2), { encoding: 'utf8', mode: 0o600 });
    },
    get(id) {
      const encoded = readMeshSecrets()[id];
      if (!encoded || !safeStorage.isEncryptionAvailable()) return '';
      try {
        return safeStorage.decryptString(Buffer.from(encoded, 'base64'));
      } catch (_error) {
        return '';
      }
    },
    has(id) {
      return Boolean(readMeshSecrets()[id]);
    }
  };
}

function getMeshRoutingService() {
  if (!meshRoutingService) {
    const meshRoot = path.join(app.getPath('userData'), 'mesh');
    meshRoutingService = new MeshRoutingService({
      configPath: path.join(meshRoot, 'config.json'),
      identityPath: path.join(meshRoot, 'identity.json'),
      secretStore: getMeshSecretStore(),
      capabilitiesProvider() {
        const route = getRouterService().currentState();
        return {
          platform: process.platform,
          architecture: process.arch,
          services: ['mesh-routing', 'lab', 'providers', 'models'],
          harnesses: ['review-act-audit'],
          providers: route.providers
            .filter((provider) => provider.health === 'healthy')
            .map((provider) => ({ type: provider.providerType, modelId: provider.modelId }))
        };
      }
    });
  }
  return meshRoutingService;
}

function labStatePath() {
  return path.join(app.getPath('userData'), 'lab-loop', 'latest.json');
}

function writeLabState(state) {
  const destination = labStatePath();
  fs.mkdirSync(path.dirname(destination), { recursive: true });
  fs.writeFileSync(destination, JSON.stringify(state, null, 2), 'utf8');
  return destination;
}

function readLabState() {
  const source = labStatePath();
  if (!fs.existsSync(source)) {
    return { ok: false, message: 'No Lab run has been completed yet.' };
  }

  try {
    return { ok: true, ...JSON.parse(fs.readFileSync(source, 'utf8')) };
  } catch (error) {
    return { ok: false, message: `Lab state could not be read: ${error.message}` };
  }
}

function tail(value, maxLength = 12000) {
  const text = String(value || '').trim();
  return text.length > maxLength ? text.slice(-maxLength) : text;
}

async function runCodexStage(stage, prompt, workspacePath, outputPath, sandbox, executable = '') {
  const result = await runCommand(executable || codexExecutable(), [
    'exec',
    '--json',
    '--ephemeral',
    '--skip-git-repo-check',
    '--sandbox',
    sandbox,
    '--cd',
    workspacePath,
    '--output-last-message',
    outputPath,
    prompt
  ], { cwd: workspacePath, timeoutMs: 10 * 60 * 1000 });

  const output = fs.existsSync(outputPath)
    ? fs.readFileSync(outputPath, 'utf8')
    : tail(result.stdout || result.stderr);

  return {
    stage,
    ok: result.ok,
    output: tail(output),
    stderr: tail(result.stderr, 4000),
    exitCode: result.exitCode
  };
}

ipcMain.handle('nexus:status', async () => ({
  app: 'NexusCentral Electron proof',
  workflow: workflowSurfaces.map((surface) => surface.name).join(' / '),
  capabilities: desktopCapabilities,
  lmStudioBaseUrl: defaultLmStudioBaseUrl,
  repoRoot
}));

ipcMain.handle('nexus:choose-workspace', async () => {
  const result = await dialog.showOpenDialog({
    title: 'Open workspace',
    properties: ['openDirectory']
  });
  if (result.canceled || result.filePaths.length === 0) {
    return { ok: false, path: '', entries: [], message: 'No workspace selected.' };
  }

  const workspacePath = result.filePaths[0];
  selectedWorkspacePath = workspacePath;
  const entries = readWorkspaceEntries(workspacePath);

  return { ok: true, path: workspacePath, entries, message: `Opened ${workspacePath}`, count: entries.length };
});

ipcMain.handle('nexus:refresh-workspace', async () => {
  if (!selectedWorkspacePath) {
    return { ok: false, path: '', entries: [], message: 'Open a workspace first.', count: 0 };
  }

  const entries = readWorkspaceEntries(selectedWorkspacePath);
  return {
    ok: true,
    path: selectedWorkspacePath,
    entries,
    message: 'Workspace refreshed.',
    count: entries.length
  };
});

ipcMain.handle('nexus:create-sandbox', async () => {
  const sandboxRoot = path.join(os.homedir(), 'Documents', 'NexusCentral', 'Sandbox');
  fs.mkdirSync(sandboxRoot, { recursive: true });
  selectedWorkspacePath = sandboxRoot;

  const entries = readWorkspaceEntries(sandboxRoot);
  return {
    ok: true,
    path: sandboxRoot,
    entries,
    message: 'Sandbox ready.',
    count: entries.length
  };
});

ipcMain.handle('nexus:rust-sidecar', async () => {
  const built = sidecarPath();
  if (fs.existsSync(built)) {
    return runCommand(built, ['--json'], { cwd: path.dirname(built) });
  }

  return runCommand('cargo', ['run', '--manifest-path', 'sidecars/rust-proof/Cargo.toml', '--', '--json'], {
    cwd: path.resolve(__dirname, '..'),
    timeoutMs: 30000
  });
});

ipcMain.handle('nexus:get-lab-state', async () => readLabState());
ipcMain.handle('nexus:router-state', async () => getRouterService().currentState());
ipcMain.handle('nexus:router-save-test', async (_event, config = {}) => getRouterService().saveAndTest(config));
ipcMain.handle('nexus:models-list', async () => getModelsService().listInstalled());
ipcMain.handle('nexus:models-search', async (_event, query = '') => getModelsService().search(query));
ipcMain.handle('nexus:models-download', async (_event, modelId = '') => getModelsService().download(modelId));
ipcMain.handle('nexus:models-load', async (_event, modelId = '') => getModelsService().load(modelId, 16000));
ipcMain.handle('nexus:models-unload', async (_event, instanceId = '') => getModelsService().unload(instanceId));
ipcMain.handle('nexus:models-publish', async (_event, modelId = '') => {
  const connection = getModelsService().getLmStudioConnection();
  return getRouterService().publishLocalModel({ baseUrl: connection.baseUrl, modelId });
});
ipcMain.handle('nexus:mesh-state', async () => getMeshRoutingService().status());
ipcMain.handle('nexus:mesh-configure', async (_event, config = {}) => getMeshRoutingService().configure(config));
ipcMain.handle('nexus:mesh-disable', async () => getMeshRoutingService().disable());
ipcMain.handle('nexus:mesh-ping', async (_event, nodeId = '') => {
  try {
    const result = await getMeshRoutingService().requestPeer(nodeId, 'mesh.ping');
    return { ok: true, result, message: 'Peer responded.' };
  } catch (error) {
    return { ok: false, message: error.message };
  }
});

ipcMain.handle('nexus:run-lab-loop', async (_event, request = {}) => {
  const task = String(request.task || '').trim();
  if (!task) {
    return { ok: false, message: 'Describe what you want to build first.' };
  }
  if (!selectedWorkspacePath || !fs.existsSync(selectedWorkspacePath)) {
    return { ok: false, message: 'Open or create a workspace before running the Lab.' };
  }

  const { buildCodingPrompt } = await import('./harnesses/coding-harness.mjs');
  const runId = new Date().toISOString().replace(/[:.]/g, '-');
  const runDirectory = path.join(app.getPath('userData'), 'lab-loop', runId);
  fs.mkdirSync(runDirectory, { recursive: true });
  const stages = [];
  const runStage = (stage, prompt, outputPath, sandbox) =>
    runCodexStage(stage, prompt, selectedWorkspacePath, outputPath, sandbox);

  const gitState = await runCommand('git', ['status', '--short', '--branch'], {
    cwd: selectedWorkspacePath,
    timeoutMs: 10000
  });
  const reviewPrompt = buildCodingPrompt('review', task, selectedWorkspacePath);
  const review = await runStage(
    'review',
    reviewPrompt,
    path.join(runDirectory, 'review.txt'),
    'read-only'
  );
  review.workspaceState = tail(gitState.stdout || gitState.stderr, 4000);
  stages.push(review);

  if (!review.ok) {
    const failed = {
      ok: false,
      runId,
      task,
      workspacePath: selectedWorkspacePath,
      artifactPath: runDirectory,
      stages,
      message: 'Review did not complete, so Act was not started.',
      finalMessage: review.output || review.stderr || 'Review failed.'
    };
    writeLabState(failed);
    return failed;
  }

  const act = await runStage(
    'act',
    buildCodingPrompt('act', task, selectedWorkspacePath, review.output),
    path.join(runDirectory, 'act.txt'),
    'workspace-write'
  );
  stages.push(act);

  if (!act.ok) {
    const failed = {
      ok: false,
      runId,
      task,
      workspacePath: selectedWorkspacePath,
      artifactPath: runDirectory,
      stages,
      message: 'Act did not complete, so Audit was not started.',
      finalMessage: act.output || act.stderr || 'Act failed.'
    };
    writeLabState(failed);
    return failed;
  }

  const audit = await runStage(
    'audit',
    buildCodingPrompt('audit', task, selectedWorkspacePath, review.output),
    path.join(runDirectory, 'audit.txt'),
    'read-only'
  );
  stages.push(audit);

  const completed = {
    ok: audit.ok,
    runId,
    task,
    workspacePath: selectedWorkspacePath,
    artifactPath: runDirectory,
    stages,
    message: audit.ok ? 'Review, Act, and Audit completed.' : 'Act completed, but Audit needs attention.',
    finalMessage: audit.output || audit.stderr || 'No audit report returned.'
  };
  writeLabState(completed);
  return completed;
});

app.whenReady().then(async () => {
  try {
    await getMeshRoutingService().start();
  } catch (error) {
    console.error(`mesh:start:failed ${error.message}`);
  }
  createWindow();
  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('before-quit', () => {
  meshRoutingService?.stop().catch((error) => console.error(`mesh:stop:failed ${error.message}`));
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});
