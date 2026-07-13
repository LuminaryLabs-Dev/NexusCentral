const crypto = require('crypto');
const dgram = require('dgram');
const fs = require('fs');
const http = require('http');
const os = require('os');
const path = require('path');
const { EventEmitter } = require('events');

const PROTOCOL_VERSION = 1;
const DEFAULT_MULTICAST_ADDRESS = '239.255.77.77';
const DEFAULT_DISCOVERY_PORT = 47777;
const DEFAULT_ANNOUNCE_INTERVAL_MS = 5000;
const DEFAULT_PEER_LEASE_MS = 16000;
const MAX_CLOCK_SKEW_MS = 30000;
const MAX_REQUEST_BYTES = 64 * 1024;

function canonicalize(value) {
  if (Array.isArray(value)) {
    return `[${value.map(canonicalize).join(',')}]`;
  }
  if (value && typeof value === 'object') {
    return `{${Object.keys(value).sort().map((key) => `${JSON.stringify(key)}:${canonicalize(value[key])}`).join(',')}}`;
  }
  return JSON.stringify(value);
}

function readJson(filePath, fallback) {
  try {
    return JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch (_error) {
    return fallback;
  }
}

function writeJson(filePath, value, mode = 0o600) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  const temporaryPath = `${filePath}.tmp`;
  fs.writeFileSync(temporaryPath, JSON.stringify(value, null, 2), { encoding: 'utf8', mode });
  fs.renameSync(temporaryPath, filePath);
}

function publicNodeId(publicKey) {
  return crypto.createHash('sha256').update(publicKey).digest('hex').slice(0, 24);
}

function safeEqual(left, right) {
  const leftBuffer = Buffer.from(String(left || ''), 'base64url');
  const rightBuffer = Buffer.from(String(right || ''), 'base64url');
  return leftBuffer.length > 0
    && leftBuffer.length === rightBuffer.length
    && crypto.timingSafeEqual(leftBuffer, rightBuffer);
}

class MeshRoutingService extends EventEmitter {
  constructor({
    configPath,
    identityPath,
    secretStore,
    capabilitiesProvider = () => ({}),
    multicastAddress = DEFAULT_MULTICAST_ADDRESS,
    discoveryPort = DEFAULT_DISCOVERY_PORT,
    announceIntervalMs = DEFAULT_ANNOUNCE_INTERVAL_MS,
    peerLeaseMs = DEFAULT_PEER_LEASE_MS,
    servicePort = 0
  }) {
    super();
    this.configPath = configPath;
    this.identityPath = identityPath;
    this.secretStore = secretStore;
    this.capabilitiesProvider = capabilitiesProvider;
    this.multicastAddress = multicastAddress;
    this.discoveryPort = discoveryPort;
    this.announceIntervalMs = announceIntervalMs;
    this.peerLeaseMs = peerLeaseMs;
    this.requestedServicePort = servicePort;
    this.identity = this.loadOrCreateIdentity();
    this.peers = new Map();
    this.actions = new Map();
    this.allowedActions = new Set(['mesh.ping', 'mesh.capabilities']);
    this.discoverySocket = null;
    this.requestServer = null;
    this.announceTimer = null;
    this.pruneTimer = null;
    this.networkKey = null;
    this.networkId = '';
    this.servicePort = 0;
    this.lastError = '';
    this.registerBuiltInActions();
  }

  defaultConfig() {
    return {
      version: PROTOCOL_VERSION,
      enabled: false,
      nodeName: os.hostname()
    };
  }

  loadConfig() {
    return { ...this.defaultConfig(), ...readJson(this.configPath, {}) };
  }

  saveConfig(config) {
    writeJson(this.configPath, config);
    return config;
  }

  loadOrCreateIdentity() {
    const saved = readJson(this.identityPath, null);
    if (saved?.publicKey && saved?.privateKey && saved.nodeId === publicNodeId(saved.publicKey)) {
      return saved;
    }

    const pair = crypto.generateKeyPairSync('ed25519');
    const identity = {
      version: PROTOCOL_VERSION,
      nodeId: '',
      publicKey: pair.publicKey.export({ type: 'spki', format: 'pem' }),
      privateKey: pair.privateKey.export({ type: 'pkcs8', format: 'pem' })
    };
    identity.nodeId = publicNodeId(identity.publicKey);
    writeJson(this.identityPath, identity);
    return identity;
  }

  registerBuiltInActions() {
    this.actions.set('mesh.ping', async () => ({ pong: true, nodeId: this.identity.nodeId, at: new Date().toISOString() }));
    this.actions.set('mesh.capabilities', async () => this.localCapabilities());
  }

  registerAction(action, handler, { allowRemote = false } = {}) {
    if (!action || typeof handler !== 'function') {
      throw new Error('Mesh actions require a name and handler.');
    }
    this.actions.set(action, handler);
    if (allowRemote) this.allowedActions.add(action);
  }

  setActionAllowed(action, allowed) {
    if (!this.actions.has(action)) throw new Error(`Unknown mesh action: ${action}`);
    if (allowed) this.allowedActions.add(action);
    else this.allowedActions.delete(action);
  }

  localCapabilities() {
    const value = this.capabilitiesProvider() || {};
    return JSON.parse(JSON.stringify(value));
  }

  deriveNetwork(lanCode) {
    const key = crypto.scryptSync(String(lanCode), 'nexuscentral-mesh-v1', 32);
    return {
      key,
      id: crypto.createHash('sha256').update(key).digest('hex').slice(0, 16)
    };
  }

  unsigned(value) {
    const { mac: _mac, signature: _signature, ...payload } = value;
    return payload;
  }

  secure(value) {
    const serialized = canonicalize(value);
    return {
      ...value,
      mac: crypto.createHmac('sha256', this.networkKey).update(serialized).digest('base64url'),
      signature: crypto.sign(null, Buffer.from(serialized), this.identity.privateKey).toString('base64url')
    };
  }

  seal(value) {
    const iv = crypto.randomBytes(12);
    const cipher = crypto.createCipheriv('aes-256-gcm', this.networkKey, iv);
    cipher.setAAD(Buffer.from(this.networkId));
    const ciphertext = Buffer.concat([cipher.update(JSON.stringify(value), 'utf8'), cipher.final()]);
    return {
      iv: iv.toString('base64url'),
      tag: cipher.getAuthTag().toString('base64url'),
      ciphertext: ciphertext.toString('base64url')
    };
  }

  open(sealed) {
    const decipher = crypto.createDecipheriv(
      'aes-256-gcm',
      this.networkKey,
      Buffer.from(sealed.iv, 'base64url')
    );
    decipher.setAAD(Buffer.from(this.networkId));
    decipher.setAuthTag(Buffer.from(sealed.tag, 'base64url'));
    const plaintext = Buffer.concat([
      decipher.update(Buffer.from(sealed.ciphertext, 'base64url')),
      decipher.final()
    ]);
    return JSON.parse(plaintext.toString('utf8'));
  }

  verify(value) {
    if (!this.networkKey || value.version !== PROTOCOL_VERSION || value.networkId !== this.networkId) return false;
    if (Math.abs(Date.now() - Number(value.timestamp || 0)) > MAX_CLOCK_SKEW_MS) return false;
    if (!value.publicKey || publicNodeId(value.publicKey) !== value.nodeId) return false;
    const serialized = canonicalize(this.unsigned(value));
    const expectedMac = crypto.createHmac('sha256', this.networkKey).update(serialized).digest('base64url');
    if (!safeEqual(value.mac, expectedMac)) return false;
    try {
      return crypto.verify(null, Buffer.from(serialized), value.publicKey, Buffer.from(value.signature, 'base64url'));
    } catch (_error) {
      return false;
    }
  }

  announcement() {
    const config = this.loadConfig();
    return this.secure({
      version: PROTOCOL_VERSION,
      type: 'announce',
      networkId: this.networkId,
      nodeId: this.identity.nodeId,
      publicKey: this.identity.publicKey,
      servicePort: this.servicePort,
      sealed: this.seal({
        nodeName: String(config.nodeName || os.hostname()).slice(0, 64),
        capabilities: this.localCapabilities()
      }),
      timestamp: Date.now(),
      nonce: crypto.randomBytes(12).toString('base64url')
    });
  }

  sendAnnouncement() {
    if (!this.discoverySocket || !this.servicePort) return;
    const message = Buffer.from(JSON.stringify(this.announcement()));
    this.discoverySocket.send(message, this.discoveryPort, this.multicastAddress);
  }

  acceptAnnouncement(message, remoteAddress) {
    let announcement;
    try {
      announcement = JSON.parse(message.toString('utf8'));
    } catch (_error) {
      return;
    }
    if (announcement.type !== 'announce' || announcement.nodeId === this.identity.nodeId || !this.verify(announcement)) return;

    let details;
    try {
      details = this.open(announcement.sealed);
    } catch (_error) {
      return;
    }

    const previous = this.peers.get(announcement.nodeId);
    this.peers.set(announcement.nodeId, {
      nodeId: announcement.nodeId,
      nodeName: details.nodeName,
      publicKey: announcement.publicKey,
      address: remoteAddress,
      servicePort: announcement.servicePort,
      capabilities: details.capabilities || {},
      lastSeenAt: Date.now()
    });
    if (!previous) this.emit('peers-changed', this.status());
  }

  prunePeers() {
    const expiredBefore = Date.now() - this.peerLeaseMs;
    let changed = false;
    for (const [nodeId, peer] of this.peers) {
      if (peer.lastSeenAt < expiredBefore) {
        this.peers.delete(nodeId);
        changed = true;
      }
    }
    if (changed) this.emit('peers-changed', this.status());
  }

  async startRequestServer() {
    this.requestServer = http.createServer((request, response) => this.handleHttpRequest(request, response));
    await new Promise((resolve, reject) => {
      this.requestServer.once('error', reject);
      this.requestServer.listen(this.requestedServicePort, '0.0.0.0', () => {
        this.requestServer.removeListener('error', reject);
        resolve();
      });
    });
    this.servicePort = this.requestServer.address().port;
  }

  async startDiscovery() {
    this.discoverySocket = dgram.createSocket({ type: 'udp4', reuseAddr: true });
    this.discoverySocket.on('message', (message, remote) => this.acceptAnnouncement(message, remote.address));
    this.discoverySocket.on('error', (error) => {
      this.lastError = error.message;
      this.emit('mesh-error', error);
    });
    await new Promise((resolve, reject) => {
      this.discoverySocket.once('error', reject);
      this.discoverySocket.bind(this.discoveryPort, '0.0.0.0', () => {
        this.discoverySocket.removeListener('error', reject);
        try {
          this.discoverySocket.addMembership(this.multicastAddress);
          this.discoverySocket.setMulticastTTL(1);
          this.discoverySocket.setMulticastLoopback(true);
          resolve();
        } catch (error) {
          reject(error);
        }
      });
    });
  }

  async start() {
    if (this.requestServer) return this.status();
    const config = this.loadConfig();
    const lanCode = this.secretStore.get('lan-code');
    if (!config.enabled || !lanCode) return this.status();

    const network = this.deriveNetwork(lanCode);
    this.networkKey = network.key;
    this.networkId = network.id;
    await this.startRequestServer();
    await this.startDiscovery();
    this.sendAnnouncement();
    this.announceTimer = setInterval(() => this.sendAnnouncement(), this.announceIntervalMs);
    this.pruneTimer = setInterval(() => this.prunePeers(), Math.max(1000, Math.floor(this.peerLeaseMs / 3)));
    this.emit('started', this.status());
    return this.status();
  }

  async stop() {
    clearInterval(this.announceTimer);
    clearInterval(this.pruneTimer);
    this.announceTimer = null;
    this.pruneTimer = null;
    const socket = this.discoverySocket;
    const server = this.requestServer;
    this.discoverySocket = null;
    this.requestServer = null;
    this.servicePort = 0;
    this.peers.clear();
    this.networkKey = null;
    this.networkId = '';
    if (socket) await new Promise((resolve) => socket.close(resolve));
    if (server) await new Promise((resolve) => server.close(resolve));
    this.emit('stopped', this.status());
    return this.status();
  }

  async configure({ lanCode = '', nodeName = '' } = {}) {
    const config = this.loadConfig();
    if (String(lanCode).trim()) this.secretStore.set('lan-code', String(lanCode));
    if (!this.secretStore.has('lan-code')) throw new Error('A LAN code is required.');
    config.enabled = true;
    config.nodeName = String(nodeName || config.nodeName || os.hostname()).trim().slice(0, 64);
    this.saveConfig(config);
    await this.stop();
    return this.start();
  }

  async disable() {
    const config = this.loadConfig();
    config.enabled = false;
    this.saveConfig(config);
    return this.stop();
  }

  status() {
    const config = this.loadConfig();
    return {
      running: Boolean(this.requestServer),
      enabled: Boolean(config.enabled),
      hasLanCode: this.secretStore.has('lan-code'),
      networkId: this.networkId,
      nodeId: this.identity.nodeId,
      nodeName: config.nodeName,
      servicePort: this.servicePort,
      lastError: this.lastError,
      peers: [...this.peers.values()].map((peer) => ({
        nodeId: peer.nodeId,
        nodeName: peer.nodeName,
        address: peer.address,
        servicePort: peer.servicePort,
        capabilities: peer.capabilities,
        lastSeenAt: new Date(peer.lastSeenAt).toISOString()
      }))
    };
  }

  requestEnvelope(peer, action, payload) {
    return this.secure({
      version: PROTOCOL_VERSION,
      type: 'request',
      requestId: crypto.randomUUID(),
      networkId: this.networkId,
      nodeId: this.identity.nodeId,
      targetNodeId: peer.nodeId,
      publicKey: this.identity.publicKey,
      sealed: this.seal({ action, payload }),
      ttl: 1,
      route: [this.identity.nodeId],
      timestamp: Date.now()
    });
  }

  async requestPeer(nodeId, action = 'mesh.ping', payload = {}) {
    const peer = this.peers.get(nodeId);
    if (!peer) throw new Error('Peer is not currently available.');
    const envelope = this.requestEnvelope(peer, action, payload);
    const response = await this.postJson(peer, envelope);
    if (!this.verify(response) || response.type !== 'response' || response.requestId !== envelope.requestId) {
      throw new Error('Peer response could not be authenticated.');
    }
    const opened = this.open(response.sealed);
    if (!opened.ok) throw new Error(opened.error || 'Peer rejected the request.');
    return opened.result;
  }

  postJson(peer, value) {
    const body = Buffer.from(JSON.stringify(value));
    return new Promise((resolve, reject) => {
      const request = http.request({
        hostname: peer.address,
        port: peer.servicePort,
        path: '/mesh/v1/request',
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Content-Length': body.length },
        timeout: 5000
      }, (response) => {
        let data = '';
        response.setEncoding('utf8');
        response.on('data', (chunk) => { data += chunk; });
        response.on('end', () => {
          try {
            resolve(JSON.parse(data));
          } catch (_error) {
            reject(new Error('Peer returned an invalid response.'));
          }
        });
      });
      request.on('timeout', () => request.destroy(new Error('Peer request timed out.')));
      request.on('error', reject);
      request.end(body);
    });
  }

  async handleHttpRequest(request, response) {
    if (request.method !== 'POST' || request.url !== '/mesh/v1/request') {
      response.writeHead(404).end();
      return;
    }
    let body = '';
    request.setEncoding('utf8');
    request.on('data', (chunk) => {
      body += chunk;
      if (Buffer.byteLength(body) > MAX_REQUEST_BYTES) request.destroy();
    });
    request.on('end', async () => {
      let envelope;
      try {
        envelope = JSON.parse(body);
      } catch (_error) {
        response.writeHead(400).end();
        return;
      }

      let ok = false;
      let result = null;
      let error = '';
      let requestData = null;
      if (!this.verify(envelope) || envelope.type !== 'request' || envelope.targetNodeId !== this.identity.nodeId || envelope.ttl < 1) {
        error = 'Request authentication failed.';
      } else {
        try {
          requestData = this.open(envelope.sealed);
        } catch (_error) {
          error = 'Request payload could not be decrypted.';
        }
      }
      if (!error && !this.allowedActions.has(requestData.action)) {
        error = 'Local policy denied this mesh action.';
      } else if (!error) {
        try {
          result = await this.actions.get(requestData.action)(requestData.payload, {
            nodeId: envelope.nodeId,
            route: envelope.route || []
          });
          ok = true;
        } catch (actionError) {
          error = actionError.message;
        }
      }

      const securedResponse = this.secure({
        version: PROTOCOL_VERSION,
        type: 'response',
        requestId: envelope.requestId || '',
        networkId: this.networkId,
        nodeId: this.identity.nodeId,
        publicKey: this.identity.publicKey,
        sealed: this.seal({ ok, result, error }),
        timestamp: Date.now()
      });
      response.writeHead(ok ? 200 : 403, { 'Content-Type': 'application/json' });
      response.end(JSON.stringify(securedResponse));
    });
  }
}

module.exports = {
  MeshRoutingService,
  DEFAULT_MULTICAST_ADDRESS,
  DEFAULT_DISCOVERY_PORT
};
