import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralWorkspaceKit() {
  return createStateKit({
    id: "n-central-workspace-kit", domain: "workspace", domainPath: "n:central:workspace", apiName: "centralWorkspace",
    name: "NexusCentral Workspace", summary: "Owns the selected host workspace, revision, and writer lease.",
    requires: ["n:central"], outputs: ["workspace.changed", "workspace.lease-changed"],
    resetPolicy: "deselect workspace without deleting files",
    initialState: { selected: null, writerLease: null },
    transitions: {
      select(state, payload) {
        state.selected = { id: String(payload.workspaceId), name: String(payload.name || "Workspace"), revision: Number(payload.revision || 0) };
        state.writerLease = null;
        return { state };
      },
      acquireWriter(state, payload) {
        if (!state.selected) throw new Error("No workspace is selected.");
        if (state.writerLease && state.writerLease.jobId !== payload.jobId) throw new Error("Workspace writer lease is held.");
        if (Number(payload.expectedRevision) !== state.selected.revision) throw new Error("Workspace revision conflict.");
        state.writerLease = { jobId: String(payload.jobId), holderId: String(payload.holderId), expiresAt: Number(payload.expiresAt) };
        return { state, result: state.writerLease };
      },
      releaseWriter(state, payload) {
        if (state.writerLease?.jobId === payload.jobId) state.writerLease = null;
        if (payload.incrementRevision && state.selected) state.selected.revision += 1;
        return { state };
      }
    }
  });
}
