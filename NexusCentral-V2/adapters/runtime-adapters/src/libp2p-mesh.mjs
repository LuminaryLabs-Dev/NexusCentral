import { networkInterfaces } from "node:os";
import { EventEmitter } from "node:events";
import { createHash, createHmac, randomUUID, scryptSync, timingSafeEqual } from "node:crypto";
import { createLibp2p } from "libp2p";
import { tcp } from "@libp2p/tcp";
import { mdns } from "@libp2p/mdns";
import { noise } from "@chainsafe/libp2p-noise";
import { yamux } from "@chainsafe/libp2p-yamux";
import { identify } from "@libp2p/identify";
import { generateKeyPair, privateKeyFromProtobuf, privateKeyToProtobuf } from "@libp2p/crypto/keys";

export class Libp2pMeshAdapter extends EventEmitter {
  constructor({ secretStore = null } = {}) { super(); this.node = null; this.membership = null; this.peers = new Map(); this.requestHandler = null; this.secretStore = secretStore; }

  async privateKey() {
    const encoded = this.secretStore?.get("mesh:libp2p-private-key");
    if (encoded) return privateKeyFromProtobuf(Buffer.from(encoded, "base64"));
    const key = await generateKeyPair("Ed25519");
    if (!this.secretStore) throw new Error("Mesh identity requires secure storage.");
    this.secretStore.set("mesh:libp2p-private-key", Buffer.from(privateKeyToProtobuf(key)).toString("base64"));
    return key;
  }

  deriveMembership(lanCode) {
    const key = scryptSync(String(lanCode), "nexuscentral-mesh-v2", 32);
    return { key, networkId: createHash("sha256").update(key).digest("hex").slice(0, 24) };
  }

  secure(body) {
    const serialized = JSON.stringify(body);
    return { networkId: this.membership.networkId, body, mac: createHmac("sha256", this.membership.key).update(serialized).digest("base64url") };
  }

  verify(envelope) {
    if (!envelope || envelope.networkId !== this.membership.networkId) return false;
    const expected = Buffer.from(createHmac("sha256", this.membership.key).update(JSON.stringify(envelope.body)).digest("base64url"));
    const actual = Buffer.from(String(envelope.mac || ""));
    return expected.length === actual.length && timingSafeEqual(expected, actual);
  }

  async start({ lanCode, addresses = ["/ip4/0.0.0.0/tcp/0"] }) {
    if (this.node) return this.status();
    try { networkInterfaces(); } catch { throw new Error("LAN discovery is unavailable: this environment prevents network interface enumeration."); }
    const membership = this.deriveMembership(lanCode);
    this.membership = membership;
    this.node = await createLibp2p({
      privateKey: await this.privateKey(),
      addresses: { listen: addresses },
      transports: [tcp()],
      connectionEncrypters: [noise()],
      streamMuxers: [yamux()],
      peerDiscovery: [mdns()],
      services: { identify: identify() }
    });
    await this.node.handle("/nexuscentral/mesh/2.0.0", async (stream, connection) => this.handleStream(stream, connection));
    this.node.addEventListener("peer:discovery", (event) => {
      this.peers.set(event.detail.id.toString(), event.detail.id);
      this.emit("peer", { peerId: event.detail.id.toString(), addresses: event.detail.multiaddrs.map(String), networkId: membership.networkId });
    });
    return this.status();
  }

  setRequestHandler(handler) { this.requestHandler = handler; }

  async readJson(stream) {
    const chunks = [];
    let length = 0;
    for await (const chunk of stream) {
      const bytes = chunk.subarray ? chunk.subarray() : chunk;
      length += bytes.byteLength;
      if (length > 1024 * 1024) throw new Error("Mesh message is too large.");
      chunks.push(bytes);
    }
    return JSON.parse(new TextDecoder().decode(Buffer.concat(chunks.map((item) => Buffer.from(item)))));
  }

  async handleStream(stream, connection) {
    try {
      const envelope = await this.readJson(stream);
      if (!this.verify(envelope)) throw new Error("Mesh membership proof failed.");
      const request = envelope.body;
      if (request.hopLimit < 1 || request.route.includes(this.node.peerId.toString())) throw new Error("Mesh route is expired or looping.");
      if (!this.requestHandler) throw new Error("Mesh execution handler is unavailable.");
      const result = await this.requestHandler(request, { peerId: connection.remotePeer.toString() });
      stream.send(new TextEncoder().encode(JSON.stringify(this.secure({ requestId: request.requestId, ok: true, result }))));
    } catch (error) {
      stream.send(new TextEncoder().encode(JSON.stringify(this.secure({ ok: false, error: error.message }))));
    } finally { await stream.close(); }
  }

  async request(peerId, action, payload, { route = [], hopLimit = 4, signal } = {}) {
    const peer = this.peers.get(peerId);
    if (!peer) throw new Error("Mesh peer is not currently discovered.");
    const stream = await this.node.dialProtocol(peer, "/nexuscentral/mesh/2.0.0", { signal });
    const request = { requestId: randomUUID(), action, payload, route: [...route, this.node.peerId.toString()], hopLimit, expiresAt: Date.now() + 30_000 };
    stream.send(new TextEncoder().encode(JSON.stringify(this.secure(request))));
    await stream.close();
    const response = await this.readJson(stream);
    if (!this.verify(response) || response.body.requestId !== request.requestId || !response.body.ok) throw new Error(response.body?.error || "Mesh request failed.");
    return response.body.result;
  }

  async stop() { if (this.node) await this.node.stop(); this.node = null; this.membership = null; this.peers.clear(); }
  status() { return { running: Boolean(this.node), peerId: this.node?.peerId.toString() || "", addresses: this.node?.getMultiaddrs().map(String) || [] }; }
}
