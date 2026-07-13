import { app, BrowserWindow, dialog, ipcMain } from "electron";
import { spawn } from "node:child_process";
import { join, resolve } from "node:path";
import WebSocket from "ws";

const root = app.isPackaged ? join(process.resourcesPath, "nexuscentral") : resolve(import.meta.dirname, "../..");
const webUrl = process.env.NEXUS_CENTRAL_URL || "http://127.0.0.1:47800";
const trustedOrigin = new URL(webUrl).origin;
if (!new Set(["127.0.0.1", "localhost", "::1"]).has(new URL(webUrl).hostname)) throw new Error("Electron may only load a loopback Nexus Node origin.");

function assertTrustedSender(event) {
  if (new URL(event.senderFrame.url).origin !== trustedOrigin) throw new Error("Untrusted Electron IPC sender.");
}

async function api(path, options) {
  const response = await fetch(`${webUrl}${path}`, options);
  const body = await response.json();
  if (!response.ok || body.ok === false) throw new Error(body.error?.message || `HTTP ${response.status}`);
  return body.data ?? body;
}

async function reachable() {
  try { const response = await fetch(`${webUrl}/api/v1/capabilities`, { signal: AbortSignal.timeout(800) }); return response.ok; }
  catch (_error) { return false; }
}

async function ensureNode() {
  if (await reachable()) return;
  const executable = process.env.NEXUS_NODE_EXECUTABLE || (app.isPackaged ? process.execPath : "node");
  const child = spawn(executable, [resolve(root, "services/nexus-node/src/cli.mjs")], { cwd: root, detached: true, stdio: "ignore", env: { ...process.env, ...(app.isPackaged ? { ELECTRON_RUN_AS_NODE: "1" } : {}), NEXUS_CENTRAL_HOST: "127.0.0.1" } });
  child.unref();
  for (let attempt = 0; attempt < 40; attempt += 1) { await new Promise((resolvePromise) => setTimeout(resolvePromise, 150)); if (await reachable()) return; }
  throw new Error("The Nexus Node service did not become ready.");
}

async function createWindow() {
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

app.whenReady().then(() => { createWindow(); app.on("activate", () => { if (!BrowserWindow.getAllWindows().length) createWindow(); }); });
app.on("window-all-closed", () => { if (process.platform !== "darwin") app.quit(); });
