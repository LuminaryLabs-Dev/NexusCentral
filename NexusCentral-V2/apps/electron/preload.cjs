const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("nexusCentral", Object.freeze({
  getCapabilities: () => ipcRenderer.invoke("nexuscentral:capabilities"),
  command: (envelope) => ipcRenderer.invoke("nexuscentral:command", envelope),
  chooseWorkspace: () => ipcRenderer.invoke("nexuscentral:choose-workspace"),
  getVersions: () => ipcRenderer.invoke("nexuscentral:versions"),
  installVersion: (version) => ipcRenderer.invoke("nexuscentral:install-version", version),
  subscribe(listener) {
    const handler = (_event, message) => listener(message);
    ipcRenderer.on("nexuscentral:event", handler);
    return () => ipcRenderer.removeListener("nexuscentral:event", handler);
  }
}));
