import { EventEmitter } from "node:events";
import { createHash, createHmac, randomUUID, scryptSync, timingSafeEqual } from "node:crypto";
import wrtc from "@roamhq/wrtc";
import WebSocket from "ws";

const MAX_FRAME_BYTES = 256 * 1024;

export class PeerJsRoomAdapter extends EventEmitter {
  constructor({ nodeId, fetchImpl = fetch } = {}) {
    super(); this.nodeId = String(nodeId || randomUUID()); this.fetchImpl = fetchImpl;
    this.peer = null; this.membership = null; this.options = null; this.peers = new Map(); this.pending = new Map(); this.active = new Map(); this.seen = new Map(); this.requestHandler = null; this.pollTimer = null;
  }

  deriveMembership(roomCode) { const key = scryptSync(String(roomCode), "nexuscentral-peerjs-v1", 32); return { key, networkId: createHash("sha256").update(key).digest("hex").slice(0, 24) }; }
  setRequestHandler(handler) { this.requestHandler = handler; }

  async start({ roomCode, brokerUrl, iceServers = [] }) {
    if (this.peer) return this.status();
    if (!roomCode || !brokerUrl) throw new Error("PeerJS room code and broker URL are required.");
    installWebRtcGlobals();
    const { Peer } = (await import("peerjs")).default;
    this.membership = this.deriveMembership(roomCode);
    const url = new URL(brokerUrl);
    const path = url.pathname.replace(/\/$/, "") || "/peer";
    const id = `nc-${this.membership.networkId}-${createHash("sha256").update(this.nodeId).digest("hex").slice(0, 16)}`;
    this.options = { id, brokerUrl: url.origin, path, peerOptions: { host: url.hostname, port: Number(url.port || (url.protocol === "https:" ? 443 : 80)), path, secure: url.protocol === "https:", serialization: "json", config: { iceServers } } };
    this.peer = new Peer(id, this.options.peerOptions);
    this.peer.on("connection", (connection) => this.#attach(connection));
    await new Promise((resolve, reject) => { const timeout = setTimeout(() => reject(new Error("PeerJS broker connection timed out.")), 10_000); this.peer.once("open", () => { clearTimeout(timeout); resolve(); }); this.peer.once("error", (error) => { clearTimeout(timeout); reject(error); }); });
    await this.discover();
    this.pollTimer = setInterval(() => this.discover().catch((error) => this.emit("transport-error", error)), 10_000); this.pollTimer.unref?.();
    return this.status();
  }

  async discover() {
    if (!this.peer || !this.options) return [];
    const endpoint = `${this.options.brokerUrl}${this.options.path}/peerjs/peers`;
    const response = await this.fetchImpl(endpoint, { signal: AbortSignal.timeout(8000) });
    if (!response.ok) throw new Error(`PeerJS discovery returned HTTP ${response.status}.`);
    const prefix = `nc-${this.membership.networkId}-`;
    const discovered = (await response.json()).filter((id) => id !== this.peer.id && String(id).startsWith(prefix));
    const now = Date.now();
    for (const peerId of discovered) {
      const current = this.peers.get(peerId) || { peerId, transportPeerId: peerId };
      this.peers.set(peerId, { ...current, lastSeenAt: now });
      this.emit("peer", { nodeId: peerId, transportPeerId: peerId, addresses: [`peerjs://${peerId}`], expiresAt: now + 30_000 });
    }
    for (const [peerId, peer] of this.peers) if (!discovered.includes(peerId) && now - peer.lastSeenAt > 30_000) this.peers.delete(peerId);
    return discovered;
  }

  async request(peerId, action, payload, { route = [], hopLimit = 4, deadlineMs = 10 * 60_000, signal, onChunk } = {}) {
    if (!this.peer || !this.peers.has(peerId)) throw new Error("PeerJS room peer is not currently discovered.");
    const connection = this.peer.connect(peerId, { serialization: "json", reliable: true, metadata: { networkId: this.membership.networkId } });
    this.#attach(connection);
    await new Promise((resolve, reject) => { const timer = setTimeout(() => reject(new Error("PeerJS data connection timed out.")), 10_000); connection.once("open", () => { clearTimeout(timer); resolve(); }); connection.once("error", (error) => { clearTimeout(timer); reject(error); }); });
    const requestId = randomUUID();
    const body = { type: "request", requestId, action, payload, route: [...route, this.nodeId], hopLimit, expiresAt: Date.now() + Math.max(1000, Math.min(10 * 60_000, Number(deadlineMs))), sender: { nodeId: this.nodeId, peerId: this.peer.id } };
    return new Promise((resolve, reject) => {
      const cleanup = () => { this.pending.delete(requestId); signal?.removeEventListener("abort", abort); setTimeout(() => connection.close(), 0); };
      const abort = () => { this.#send(connection, { type: "cancel", requestId }); cleanup(); reject(new Error("PeerJS request cancelled.")); };
      this.pending.set(requestId, { peerId, resolve: (value) => { cleanup(); resolve(value); }, reject: (error) => { cleanup(); reject(error); }, onChunk });
      signal?.addEventListener("abort", abort, { once: true });
      this.#send(connection, body);
    });
  }

  #attach(connection) {
    if (connection.__nexusAttached) return; connection.__nexusAttached = true;
    connection.on("data", (envelope) => this.#receive(connection, envelope).catch((error) => this.#send(connection, { type: "error", requestId: envelope?.body?.requestId || "", error: error.message })));
    connection.on("error", (error) => this.#rejectPeer(connection.peer, error));
    connection.on("close", () => this.#rejectPeer(connection.peer, new Error("PeerJS connection closed.")));
  }

  async #receive(connection, envelope) {
    const body = this.#verify(envelope);
    if (["chunk", "finish", "error"].includes(body.type)) {
      const pending = this.pending.get(body.requestId); if (!pending) return;
      if (body.type === "chunk") { await pending.onChunk?.(String(body.content || ""), Number(body.sequence)); return; }
      if (body.type === "finish") pending.resolve(body.result);
      else pending.reject(new Error(body.error || "PeerJS request failed."));
      return;
    }
    if (body.type === "cancel") { this.active.get(body.requestId)?.abort(); return; }
    if (body.type !== "request") throw new Error("Unsupported PeerJS frame.");
    if (body.expiresAt <= Date.now() || body.hopLimit < 1 || body.route.includes(this.nodeId)) throw new Error("PeerJS request is expired or looping.");
    if (this.seen.has(body.requestId)) throw new Error("PeerJS request replay rejected.");
    this.seen.set(body.requestId, body.expiresAt); for (const [id, expiry] of this.seen) if (expiry <= Date.now()) this.seen.delete(id);
    if (!this.requestHandler) throw new Error("PeerJS execution handler is unavailable.");
    const controller = new AbortController(); this.active.set(body.requestId, controller); let sequence = 0;
    try {
      const result = await this.requestHandler(body, { peerId: body.sender?.nodeId || connection.peer, transportPeerId: connection.peer }, { signal: controller.signal, onChunk: async (content) => this.#send(connection, { type: "chunk", requestId: body.requestId, sequence: sequence++, content }) });
      this.#send(connection, { type: "finish", requestId: body.requestId, result });
    } finally { this.active.delete(body.requestId); }
  }

  #send(connection, body) { const envelope = this.#secure(body); if (Buffer.byteLength(JSON.stringify(envelope)) > MAX_FRAME_BYTES) throw new Error("PeerJS frame exceeds 256 KiB."); connection.send(envelope); }
  #secure(body) { const serialized = JSON.stringify(body); return { networkId: this.membership.networkId, body, mac: createHmac("sha256", this.membership.key).update(serialized).digest("base64url") }; }
  #verify(envelope) {
    if (!envelope || envelope.networkId !== this.membership?.networkId) throw new Error("PeerJS room membership proof failed.");
    const serialized = JSON.stringify(envelope.body); const expected = Buffer.from(createHmac("sha256", this.membership.key).update(serialized).digest("base64url")); const actual = Buffer.from(String(envelope.mac || ""));
    if (expected.length !== actual.length || !timingSafeEqual(expected, actual)) throw new Error("PeerJS room membership proof failed.");
    return envelope.body;
  }
  #rejectPeer(peerId, error) { for (const pending of this.pending.values()) if (pending.peerId === peerId) pending.reject(error); this.peers.delete(peerId); }

  async stop() { if (this.pollTimer) clearInterval(this.pollTimer); this.pollTimer = null; for (const active of this.active.values()) active.abort(); this.active.clear(); this.pending.clear(); this.peer?.destroy(); this.peer = null; this.membership = null; this.options = null; this.peers.clear(); }
  status() { return { running: Boolean(this.peer?.open), peerId: this.peer?.id || "", brokerUrl: this.options?.brokerUrl || "", path: this.options?.path || "", discoveredPeers: this.peers.size }; }
}

function installWebRtcGlobals() {
  Object.assign(globalThis, { window: globalThis, WebSocket, RTCPeerConnection: wrtc.RTCPeerConnection, RTCSessionDescription: wrtc.RTCSessionDescription, RTCIceCandidate: wrtc.RTCIceCandidate });
}
