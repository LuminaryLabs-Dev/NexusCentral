import { createServer as createHttpServer } from "node:http";
import { createServer as createHttpsServer } from "node:https";
import { readFileSync } from "node:fs";
import express from "express";
import { ExpressPeerServer } from "peer";

export class PeerRoomBroker {
  constructor({ host = "127.0.0.1", port = 47900, path = "/peer", tls = null } = {}) {
    this.host = host; this.port = port; this.path = path.startsWith("/") ? path : `/${path}`; this.tls = tls; this.server = null;
  }

  async start() {
    if (this.server) return this.address();
    const app = express(); app.disable("x-powered-by");
    app.get("/health", (_request, response) => response.json({ ok: true, service: "nexuscentral-peer-broker" }));
    this.server = this.tls
      ? createHttpsServer({ cert: readFileSync(this.tls.certPath), key: readFileSync(this.tls.keyPath) }, app)
      : createHttpServer(app);
    app.use(this.path, ExpressPeerServer(this.server, { path: "/", allow_discovery: true, concurrent_limit: 5000, alive_timeout: 60_000, expire_timeout: 5000 }));
    await new Promise((resolve, reject) => { this.server.once("error", reject); this.server.listen(this.port, this.host, resolve); });
    return this.address();
  }

  address() { const address = this.server?.address(); const port = typeof address === "object" && address ? address.port : this.port; return { protocol: this.tls ? "https" : "http", host: this.host, port, path: this.path, url: `${this.tls ? "https" : "http"}://${this.host}:${port}${this.path}` }; }
  async close() { if (!this.server) return; const server = this.server; this.server = null; server.closeAllConnections?.(); await new Promise((resolve) => server.close(resolve)); }
}
