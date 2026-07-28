import { EventEmitter } from "node:events";

export class MeshTransportCoordinator extends EventEmitter {
  constructor(transports = {}) {
    super(); this.transports = new Map(Object.entries(transports));
    for (const [name, transport] of this.transports) {
      transport.on("peer", (peer) => this.emit("peer", { ...peer, transport: name }));
      transport.on("transport-error", (error) => this.emit("transport-error", { transport: name, error }));
    }
  }

  setRequestHandler(handler) { for (const [name, transport] of this.transports) transport.setRequestHandler((request, peer, stream) => handler(request, { ...peer, transport: name }, stream)); }
  async start({ transport = "lan", ...options }) { const adapter = this.transports.get(transport); if (!adapter) throw new Error(`Unknown mesh transport: ${transport}.`); return { transport, ...(await adapter.start(options)) }; }
  async stop(transport) { if (transport) return this.transports.get(transport)?.stop(); await Promise.all([...this.transports.values()].map((adapter) => adapter.stop())); }
  async request(peer, action, payload, options = {}) { const transport = peer.transport || options.transport || "lan"; const adapter = this.transports.get(transport); if (!adapter) throw new Error(`Mesh transport ${transport} is unavailable.`); return adapter.request(peer.transportPeerId || peer.nodeId || peer, action, payload, options); }
  deriveMembership(code, transport = "lan") { const adapter = this.transports.get(transport); if (!adapter) throw new Error(`Mesh transport ${transport} is unavailable.`); return adapter.deriveMembership(code); }
  status() { return Object.fromEntries([...this.transports].map(([name, adapter]) => [name, adapter.status()])); }
}
