import { app, BrowserWindow, dialog, ipcMain } from "electron";
import { spawn } from "node:child_process";
import { cpSync, existsSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, renameSync, rmSync, symlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import WebSocket from "ws";

const bundledRoot = app.isPackaged ? join(process.resourcesPath, "nexuscentral") : resolve(import.meta.dirname, "../..");
const webUrl = process.env.NEXUS_CENTRAL_URL || "http://127.0.0.1:47800";
const trustedOrigin = new URL(webUrl).origin;
const registryUrl = "https://raw.githubusercontent.com/LuminaryLabs-Dev/NexusCentral/main/updates/update-registry.json";
let runtimeRoot = bundledRoot;
let runtimeState = null;
let nodeProcess = null;

if (!new Set(["127.0.0.1", "localhost", "::1"]).has(new URL(webUrl).hostname)) throw new Error("Electron may only load a loopback Nexus Node origin.");

function assertTrustedSender(event) {
  if (new URL(event.senderFrame.url).origin !== trustedOrigin) throw new Error("Untrusted Electron IPC sender.");
}

function statePath() {
  return join(app.getPath("userData"), "current.json");
}

function runtimeDirectory(version) {
  return join(app.getPath("userData"), "runtime", version);
}

function readRuntimeState() {
  if (!app.isPackaged) return { active: "main", channel: "main" };
  try { return { active: app.getVersion(), channel: "stable", ...JSON.parse(readFileSync(statePath(), "utf8")) }; }
  catch (_error) { return { active: app.getVersion(), channel: "stable" }; }
}

function writeRuntimeState() {
  mkdirSync(app.getPath("userData"), { recursive: true });
  writeFileSync(statePath(), `${JSON.stringify(runtimeState, null, 2)}\n`);
}

function selectRuntimeRoot() {
  runtimeState = readRuntimeState();
  const installed = runtimeDirectory(runtimeState.active);
  if (app.isPackaged && existsSync(join(installed, "services", "nexus-node", "src", "cli.mjs"))) runtimeRoot = installed;
  else {
    runtimeRoot = bundledRoot;
    if (app.isPackaged && runtimeState.active !== app.getVersion()) {
      runtimeState = { active: app.getVersion(), channel: "stable" };
      writeRuntimeState();
    }
  }
}

async function registry() {
  const response = await fetch(registryUrl, { signal: AbortSignal.timeout(10000), cache: "no-store" });
  if (!response.ok) throw new Error(`Update registry unavailable: HTTP ${response.status}`);
  const value = await response.json();
  if (value?.schemaVersion !== 1 || !Array.isArray(value.versions) || !value.channels) throw new Error("Update registry is invalid.");
  return value;
}

function platformId() {
  if (process.platform === "darwin" && process.arch === "arm64") return "macos-arm64";
  if (process.platform === "darwin" && process.arch === "x64") return "macos-x64";
  if (process.platform === "win32" && process.arch === "x64") return "windows-x64";
  if (process.platform === "linux" && process.arch === "x64") return "linux-x64";
  return `${process.platform}-${process.arch}`;
}

function bootstrapSupports(minimum) {
  const current = app.getVersion().split(".").map(Number);
  const required = minimum.split(".").map(Number);
  for (let index = 0; index < 3; index += 1) {
    if ((current[index] || 0) > (required[index] || 0)) return true;
    if ((current[index] || 0) < (required[index] || 0)) return false;
  }
  return true;
}

async function reachable() {
  try { const response = await fetch(`${webUrl}/api/v1/capabilities`, { signal: AbortSignal.timeout(800) }); return response.ok; }
  catch (_error) { return false; }
}

async function stopManagedNode() {
  if (!(await reachable())) return;
  if (!nodeProcess?.pid) throw new Error("Stop the always-running Nexus Node service before switching NexusCentral versions.");
  try { process.kill(nodeProcess.pid, "SIGTERM"); } catch (_error) {}
  for (let attempt = 0; attempt < 30; attempt += 1) {
    if (!(await reachable())) break;
    await new Promise((resolvePromise) => setTimeout(resolvePromise, 100));
  }
  if (await reachable()) throw new Error("Nexus Node did not stop cleanly.");
  nodeProcess = null;
}

async function installRuntime(entry) {
  if (!app.isPackaged) throw new Error("Version installation is available from a packaged NexusCentral app.");
  if (!entry?.installable || entry.runtime?.type !== "esm" || entry.runtime?.root !== "NexusCentral-V2") throw new Error("This NexusCentral version is not installable.");
  if (!/^(main|\d+\.\d+\.\d+)$/.test(entry.ref) || entry.version !== entry.ref) throw new Error("Update registry contains an invalid version ref.");
  if (!entry.platforms?.includes(platformId())) throw new Error(`NexusCentral ${entry.version} is not available for ${platformId()}.`);
  if (!bootstrapSupports(entry.minimumBootstrap || "0.1.0")) throw new Error(`NexusCentral ${entry.version} requires bootstrap ${entry.minimumBootstrap} or newer.`);

  const temporary = mkdtempSync(join(tmpdir(), "nexuscentral-runtime-"));
  const archive = join(temporary, "runtime.tar.gz");
  const stage = join(app.getPath("userData"), "runtime", `.${entry.version}-staging`);
  const target = runtimeDirectory(entry.version);
  const backup = join(app.getPath("userData"), "runtime", `.${entry.version}-previous`);

  try {
    const response = await fetch(`https://codeload.github.com/LuminaryLabs-Dev/NexusCentral/tar.gz/refs/heads/${encodeURIComponent(entry.ref)}`, { signal: AbortSignal.timeout(60000), cache: "no-store" });
    if (!response.ok) throw new Error(`Unable to download NexusCentral ${entry.version}: HTTP ${response.status}`);
    writeFileSync(archive, Buffer.from(await response.arrayBuffer()));

    await new Promise((resolvePromise, rejectPromise) => {
      const extraction = spawn("tar", ["-xzf", archive, "-C", temporary], { windowsHide: true, stdio: "ignore" });
      extraction.on("error", rejectPromise);
      extraction.on("exit", (code) => code === 0 ? resolvePromise() : rejectPromise(new Error("Unable to extract the NexusCentral runtime package.")));
    });

    const repositoryRoot = readdirSync(temporary, { withFileTypes: true }).find((item) => item.isDirectory());
    if (!repositoryRoot) throw new Error("Downloaded NexusCentral runtime is empty.");
    const source = join(temporary, repositoryRoot.name, entry.runtime.root);
    if (!existsSync(join(source, "services", "nexus-node", "src", "cli.mjs"))) throw new Error("Downloaded NexusCentral runtime is incomplete.");

    rmSync(stage, { recursive: true, force: true });
    mkdirSync(stage, { recursive: true });
    for (const name of ["adapters", "kits", "packages", "services"]) cpSync(join(source, name), join(stage, name), { recursive: true });
    cpSync(join(source, "package.json"), join(stage, "package.json"));

    const moduleRoot = join(stage, "node_modules");
    const bundledModules = join(bundledRoot, "node_modules");
    mkdirSync(moduleRoot, { recursive: true });
    for (const item of readdirSync(bundledModules, { withFileTypes: true })) {
      if (item.name.startsWith(".") || item.name === "@nexuscentral") continue;
      symlinkSync(join(bundledModules, item.name), join(moduleRoot, item.name), process.platform === "win32" ? "junction" : "dir");
    }

    const nexusModules = join(moduleRoot, "@nexuscentral");
    mkdirSync(nexusModules, { recursive: true });
    for (const group of ["adapters", "kits", "packages", "services"]) {
      const groupRoot = join(stage, group);
      if (!existsSync(groupRoot)) continue;
      for (const item of readdirSync(groupRoot, { withFileTypes: true })) {
        if (!item.isDirectory()) continue;
        const workspace = join(groupRoot, item.name);
        const packageFile = join(workspace, "package.json");
        if (!existsSync(packageFile)) continue;
        const name = JSON.parse(readFileSync(packageFile, "utf8")).name;
        if (typeof name !== "string" || !name.startsWith("@nexuscentral/")) continue;
        symlinkSync(workspace, join(nexusModules, name.slice("@nexuscentral/".length)), process.platform === "win32" ? "junction" : "dir");
      }
    }

    mkdirSync(join(stage, "apps", "web"), { recursive: true });
    symlinkSync(join(bundledRoot, "apps", "web", "out"), join(stage, "apps", "web", "out"), process.platform === "win32" ? "junction" : "dir");
    writeFileSync(join(stage, "runtime.json"), `${JSON.stringify({ version: entry.version, ref: entry.ref, type: "esm" }, null, 2)}\n`);

    await stopManagedNode();
    rmSync(backup, { recursive: true, force: true });
    if (existsSync(target)) renameSync(target, backup);
    try { renameSync(stage, target); }
    catch (error) {
      if (existsSync(backup)) renameSync(backup, target);
      throw error;
    }
    rmSync(backup, { recursive: true, force: true });
    runtimeState = {
      active: entry.version,
      channel: entry.status === "development" ? "main" : entry.status
    };
    runtimeRoot = target;
    writeRuntimeState();
    return { active: runtimeState.active, channel: runtimeState.channel };
  }
  finally {
    rmSync(stage, { recursive: true, force: true });
    rmSync(temporary, { recursive: true, force: true });
  }
}

async function api(path, options) {
  const response = await fetch(`${webUrl}${path}`, options);
  const body = await response.json();
  if (!response.ok || body.ok === false) throw new Error(body.error?.message || `HTTP ${response.status}`);
  return body.data ?? body;
}

async function ensureNode() {
  if (await reachable()) return;
  const executable = process.env.NEXUS_NODE_EXECUTABLE || (app.isPackaged ? process.execPath : "node");
  const child = spawn(executable, [resolve(runtimeRoot, "services/nexus-node/src/cli.mjs")], { cwd: runtimeRoot, detached: true, stdio: "ignore", env: { ...process.env, ...(app.isPackaged ? { ELECTRON_RUN_AS_NODE: "1" } : {}), NEXUS_CENTRAL_HOST: "127.0.0.1" } });
  nodeProcess = child;
  child.once("exit", () => { if (nodeProcess === child) nodeProcess = null; });
  child.unref();
  for (let attempt = 0; attempt < 40; attempt += 1) { await new Promise((resolvePromise) => setTimeout(resolvePromise, 150)); if (await reachable()) return; }
  throw new Error("The Nexus Node service did not become ready.");
}

async function createWindow() {
  selectRuntimeRoot();
  if (app.isPackaged && runtimeState.channel === "main") {
    try {
      const value = await registry();
      const entry = value.versions.find((item) => item.version === "main" && item.installable);
      if (entry) await installRuntime(entry);
    }
    catch (_error) {}
  }
  try { await ensureNode(); }
  catch (error) { await dialog.showMessageBox({ type: "error", title: "NexusCentral", message: error.message }); app.quit(); return; }
  const window = new BrowserWindow({ width: 1420, height: 920, minWidth: 920, minHeight: 640, backgroundColor: "#06251f", title: "NexusCentral", webPreferences: { preload: join(import.meta.dirname, "preload.cjs"), contextIsolation: true, nodeIntegration: false, sandbox: true } });
  window.webContents.setWindowOpenHandler(() => ({ action: "deny" }));
  window.webContents.on("will-navigate", (event, url) => { if (new URL(url).origin !== trustedOrigin) event.preventDefault(); });
  await window.loadURL(webUrl);
  attachEvents(window);
}

function attachEvents(window) {
  let socket;
  const connect = () => {
    if (window.isDestroyed()) return;
    socket = new WebSocket(webUrl.replace(/^http/, "ws") + "/api/v1/events");
    socket.on("message", (message) => window.webContents.send("nexuscentral:event", JSON.parse(message.toString())));
    socket.on("close", () => { if (!window.isDestroyed()) setTimeout(connect, 1000); });
  };
  connect();
  window.on("closed", () => socket?.close());
}

ipcMain.handle("nexuscentral:capabilities", (event) => { assertTrustedSender(event); return api("/api/v1/capabilities"); });
ipcMain.handle("nexuscentral:command", (event, envelope) => { assertTrustedSender(event); return api("/api/v1/commands", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify(envelope) }); });
ipcMain.handle("nexuscentral:choose-workspace", async (event) => {
  assertTrustedSender(event);
  const result = await dialog.showOpenDialog(BrowserWindow.getFocusedWindow(), { properties: ["openDirectory", "createDirectory"] });
  return result.canceled ? null : result.filePaths[0];
});
ipcMain.handle("nexuscentral:versions", async (event) => {
  assertTrustedSender(event);
  const value = await registry();
  return { active: runtimeState?.active || (app.isPackaged ? app.getVersion() : "main"), channel: runtimeState?.channel || (app.isPackaged ? "stable" : "main"), recommended: value.recommended, canInstall: app.isPackaged, versions: value.versions };
});
ipcMain.handle("nexuscentral:install-version", async (event, version) => {
  assertTrustedSender(event);
  const value = await registry();
  const entry = value.versions.find((item) => item.version === version);
  if (!entry) throw new Error(`Unknown NexusCentral version: ${version}`);
  const result = await installRuntime(entry);
  setTimeout(() => { app.relaunch(); app.exit(0); }, 150);
  return result;
});

app.whenReady().then(() => { createWindow(); app.on("activate", () => { if (!BrowserWindow.getAllWindows().length) createWindow(); }); });
app.on("window-all-closed", () => { if (process.platform !== "darwin") app.quit(); });
