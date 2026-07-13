import { createStateKit } from "../_shared/create-state-kit.mjs";

export function createCentralWorkspaceTransferKit() {
  return createStateKit({
    id: "n-central-workspace-transfer-kit", domain: "workspace-transfer", domainPath: "n:central:workspace-transfer", apiName: "centralWorkspaceTransfer",
    name: "NexusCentral Workspace Transfer", summary: "Owns content snapshot transfer plans and chunk receipts.",
    requires: ["n:central:workspace", "n:central:artifacts", "n:central:jobs", "n:central:policy"], outputs: ["workspace-transfer.changed"], resetPolicy: "clear incomplete transfer plans without deleting content",
    initialState: { transfers: [] },
    transitions: {
      plan(state, payload) {
        const transfer = { id: String(payload.id), snapshotHash: String(payload.snapshotHash), targetNodeId: String(payload.targetNodeId), chunks: Number(payload.chunks), received: [], status: "planned", conflicts: [] };
        state.transfers.push(transfer);
        return { state, result: transfer };
      },
      receiveChunk(state, payload) {
        const transfer = state.transfers.find((item) => item.id === payload.id);
        if (!transfer) throw new Error("Unknown transfer.");
        if (!transfer.received.includes(Number(payload.index))) transfer.received.push(Number(payload.index));
        if (transfer.received.length === transfer.chunks) transfer.status = "received";
        return { state, result: transfer };
      },
      finish(state, payload) {
        const transfer = state.transfers.find((item) => item.id === payload.id);
        if (!transfer || transfer.status !== "received") throw new Error("Transfer is incomplete.");
        transfer.status = "completed";
        return { state, result: transfer };
      }
    },
    reset(state, empty) {
      return { ...empty, transfers: state.transfers.filter((item) => item.status === "completed") };
    }
  });
}
