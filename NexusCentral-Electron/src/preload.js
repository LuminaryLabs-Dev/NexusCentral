const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('nexusCentral', {
  getStatus: () => ipcRenderer.invoke('nexus:status'),
  chooseWorkspace: () => ipcRenderer.invoke('nexus:choose-workspace'),
  refreshWorkspace: () => ipcRenderer.invoke('nexus:refresh-workspace'),
  createSandbox: () => ipcRenderer.invoke('nexus:create-sandbox'),
  runRustSidecar: () => ipcRenderer.invoke('nexus:rust-sidecar'),
  runLabLoop: (request) => ipcRenderer.invoke('nexus:run-lab-loop', request),
  getLabState: () => ipcRenderer.invoke('nexus:get-lab-state'),
  getRouterState: () => ipcRenderer.invoke('nexus:router-state'),
  saveAndTestRouter: (config) => ipcRenderer.invoke('nexus:router-save-test', config),
  listModels: () => ipcRenderer.invoke('nexus:models-list'),
  searchModels: (query) => ipcRenderer.invoke('nexus:models-search', query),
  downloadModel: (modelId) => ipcRenderer.invoke('nexus:models-download', modelId),
  loadModel: (modelId) => ipcRenderer.invoke('nexus:models-load', modelId),
  unloadModel: (instanceId) => ipcRenderer.invoke('nexus:models-unload', instanceId),
  publishModelProvider: (modelId) => ipcRenderer.invoke('nexus:models-publish', modelId),
  getMeshState: () => ipcRenderer.invoke('nexus:mesh-state'),
  configureMesh: (config) => ipcRenderer.invoke('nexus:mesh-configure', config),
  disableMesh: () => ipcRenderer.invoke('nexus:mesh-disable'),
  pingMeshPeer: (nodeId) => ipcRenderer.invoke('nexus:mesh-ping', nodeId)
});
