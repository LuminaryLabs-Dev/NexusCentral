import { createReadStream, existsSync, readFileSync, statSync } from "node:fs";
import { randomBytes } from "node:crypto";
import { createServer as createHttpServer } from "node:http";
import { createServer as createHttpsServer } from "node:https";
import { basename, extname, isAbsolute, join, normalize, relative, resolve, sep } from "node:path";
import { WebSocketServer } from "ws";
import { API_VERSION, commandEnvelope, publicError } from "@nexuscentral/contracts";

const MIME = { ".html": "text/html; charset=utf-8", ".js": "text/javascript; charset=utf-8", ".mjs": "text/javascript; charset=utf-8", ".css": "text/css; charset=utf-8", ".json": "application/json", ".wasm": "application/wasm", ".svg": "image/svg+xml" };

function cookies(request) {
  return Object.fromEntries(String(request.headers.cookie || "").split(";").map((item) => item.trim().split("=")).filter(([key]) => key));
}

function isLoopback(address = "") {
  return address === "127.0.0.1" || address === "::1" || address === "::ffff:127.0.0.1";
}

async function readJson(request, maxBytes = 1024 * 1024) {
  let body = "";
  for await (const chunk of request) {
    body += chunk;
    if (Buffer.byteLength(body) > maxBytes) throw new Error("Request is too large.");
  }
  return body ? JSON.parse(body) : {};
}

function json(response, status, body, headers = {}) {
  response.writeHead(status, { "Content-Type": "application/json; charset=utf-8", "Cache-Control": "no-store", ...headers });
  response.end(JSON.stringify(body));
}

export class LanServer {
  constructor({ node, staticRoot, host = "127.0.0.1", port = 47800, tls = null }) {
    this.node = node; this.staticRoot = resolve(staticRoot); this.host = host; this.port = port; this.tls = tls;
    this.server = null; this.webSockets = new WebSocketServer({ noServer: true }); this.connections = new Map(); this.workerJobs = new Map(); this.gatewayTokens = new Map();
    this.node.setBrowserDispatcher((sessionId, offer, onChunk, signal) => this.dispatchBrowser(sessionId, offer, onChunk, signal));
  }

  context(request) {
    if (isLoopback(request.socket.remoteAddress)) return { actorId: "local-host", scopes: ["admin"] };
    const session = this.node.getSession(cookies(request).nexus_session);
    return session ? { actorId: session.id, scopes: session.scopes } : { actorId: "anonymous", scopes: [] };
  }

  async start() {
    const listener = (request, response) => this.handle(request, response).catch((error) => json(response, 500, { ok: false, error: publicError(error) }));
    this.server = this.tls ? createHttpsServer({ key: readFileSync(this.tls.keyPath), cert: readFileSync(this.tls.certPath) }, listener) : createHttpServer(listener);
    this.server.on("upgrade", (request, socket, head) => {
      const context = this.context(request);
      if (!context.scopes.length || request.url !== "/api/v1/events") { socket.destroy(); return; }
      this.webSockets.handleUpgrade(request, socket, head, (ws) => this.attachSocket(ws, context));
    });
    this.node.on("event", (event) => this.broadcast(event));
    await new Promise((resolvePromise, reject) => { this.server.once("error", reject); this.server.listen(this.port, this.host, resolvePromise); });
    this.node.setCodexGateway({ issueAccess: (spec) => this.issueGatewayAccess(spec), revokeAccess: (token) => this.gatewayTokens.delete(token) });
    return this.address();
  }

  address() {
    const address = this.server.address();
    return { protocol: this.tls ? "https" : "http", host: this.host, port: address.port, url: `${this.tls ? "https" : "http"}://${this.host}:${address.port}` };
  }

  async handle(request, response) {
    const url = new URL(request.url, "http://local");
    const origin = request.headers.origin;
    if (origin && new URL(origin).host !== request.headers.host) return json(response, 403, { ok: false, error: { code: "ORIGIN_REJECTED", message: "Cross-origin host commands are not allowed." } });
    if (request.method === "GET" && url.pathname === "/api/v1/capabilities") return json(response, 200, this.node.capabilities());
    if (request.method === "GET" && url.pathname === "/v1/models") {
      const access = this.gatewayAccess(request); if (!access) return this.gatewayError(response, 401, "Invalid or expired gateway token.");
      const profiles = this.node.engine.n.centralProviders.getSnapshot().profiles.filter((item) => item.enabled !== false && item.health === "healthy" && item.modelId);
      return json(response, 200, { object: "list", data: profiles.map((item) => ({ id: item.modelId, object: "model", owned_by: "nexuscentral" })) });
    }
    if (request.method === "POST" && url.pathname === "/v1/chat/completions") {
      const access = this.gatewayAccess(request); if (!access) return this.gatewayError(response, 401, "Invalid or expired gateway token.");
      return this.handleGatewayChat(request, response, access);
    }
    if (request.method === "GET" && url.pathname === "/api/v1/state") {
      const context = this.context(request); this.node.requireScope(context, "view");
      return json(response, 200, { ok: true, data: this.node.publicState(context) });
    }
    if (request.method === "POST" && url.pathname === "/api/v1/pair") {
      const body = await readJson(request);
      const session = await this.node.execute(commandEnvelope("lan.pair", body), { actorId: "anonymous", scopes: [] });
      return json(response, 200, { ok: true, data: { scopes: session.scopes, expiresAt: session.expiresAt } }, { "Set-Cookie": `nexus_session=${session.id}; HttpOnly; SameSite=Strict; Path=/; Max-Age=86400${this.tls ? "; Secure" : ""}` });
    }
    if (request.method === "POST" && url.pathname === "/api/v1/commands") {
      const context = this.context(request);
      if (!context.scopes.length) return json(response, 401, { ok: false, error: { code: "UNAUTHORIZED", message: "Pair with the host first." } });
      const envelope = await readJson(request);
      try { return json(response, 200, { ok: true, data: await this.node.execute(envelope, context) }); }
      catch (error) { return json(response, 400, { ok: false, error: publicError(error, "COMMAND_REJECTED") }); }
    }
    if (request.method === "GET" && url.pathname.startsWith("/api/v1/jobs/")) {
      const context = this.context(request); this.node.requireScope(context, "view");
      const id = url.pathname.split("/").at(-1);
      const job = this.node.engine.n.centralJobs.getSnapshot().jobs.find((item) => item.id === id);
      return job ? json(response, 200, { ok: true, data: job }) : json(response, 404, { ok: false, error: { code: "NOT_FOUND", message: "Job not found." } });
    }
    if (request.method === "GET" && url.pathname.startsWith("/api/v1/artifacts/")) {
      const context = this.context(request); this.node.requireScope(context, "view");
      try {
        const id = decodeURIComponent(url.pathname.slice("/api/v1/artifacts/".length));
        const file = this.node.artifactFile(id);
        response.writeHead(200, { "Content-Type": file.mediaType, "Content-Length": file.size, "Content-Disposition": `attachment; filename="${basename(file.path).replaceAll('"', "")}"`, "Cache-Control": "private, no-store", "X-Content-Type-Options": "nosniff" });
        return createReadStream(file.path).pipe(response);
      } catch (error) { return json(response, 404, { ok: false, error: { code: "NOT_FOUND", message: error.message } }); }
    }
    return this.serveStatic(url.pathname, response);
  }

  issueGatewayAccess(spec) {
    for (const [token, access] of this.gatewayTokens) if (access.expiresAt <= Date.now()) this.gatewayTokens.delete(token);
    const token = randomBytes(32).toString("base64url");
    const address = this.address();
    const access = { token, runId: String(spec.runId), allowedTools: [...(spec.allowedTools || [])], expiresAt: Number(spec.expiresAt || Date.now() + 10 * 60_000) };
    this.gatewayTokens.set(token, access);
    return { ...access, baseUrl: `${address.protocol}://127.0.0.1:${address.port}/v1`, modelId: "nexuscentral-routed" };
  }

  gatewayAccess(request) {
    const token = String(request.headers.authorization || "").replace(/^Bearer\s+/i, "");
    const access = this.gatewayTokens.get(token);
    if (!access || access.expiresAt <= Date.now()) { if (token) this.gatewayTokens.delete(token); return null; }
    return access;
  }

  async handleGatewayChat(request, response, access) {
    const body = await readJson(request, 8 * 1024 * 1024);
    const controller = new AbortController();
    response.on("close", () => { if (!response.writableEnded) controller.abort(); });
    try {
      await this.node.streamCodexGateway(body, access, {
        onReady: ({ contentType }) => response.writeHead(200, { "Content-Type": contentType, "Cache-Control": "no-store", "X-Content-Type-Options": "nosniff" }),
        onChunk: (bytes) => new Promise((resolvePromise, reject) => response.write(bytes, (error) => error ? reject(error) : resolvePromise()))
      }, controller.signal);
      response.end();
    } catch (error) {
      if (!response.headersSent) return this.gatewayError(response, 502, error.message);
      response.end();
    }
  }

  gatewayError(response, status, message) { return json(response, status, { error: { message, type: "nexuscentral_gateway_error" } }); }

  serveStatic(pathname, response) {
    const requested = pathname === "/" ? "index.html" : normalize(pathname).replace(/^[/\\]+/, "");
    let filePath = join(this.staticRoot, requested);
    const rel = relative(this.staticRoot, filePath);
    if (rel === ".." || rel.startsWith(`..${sep}`) || isAbsolute(rel)) return response.writeHead(403).end();
    if (!existsSync(filePath) || statSync(filePath).isDirectory()) filePath = join(this.staticRoot, "index.html");
    if (!existsSync(filePath)) return json(response, 503, { ok: false, error: { code: "WEB_NOT_BUILT", message: "Build apps/web before starting Nexus Central Web." } });
    response.writeHead(200, { "Content-Type": MIME[extname(filePath)] || "application/octet-stream", "X-Content-Type-Options": "nosniff", "Referrer-Policy": "no-referrer" });
    createReadStream(filePath).pipe(response);
  }

  attachSocket(ws, context) {
    const sockets = this.connections.get(context.actorId) || new Set(); sockets.add(ws); this.connections.set(context.actorId, sockets);
    ws.send(JSON.stringify({ type: "session.ready", data: { actorId: context.actorId, scopes: context.scopes }, at: Date.now() }));
    ws.on("message", (data) => this.handleWorkerMessage(context, JSON.parse(data.toString())));
    ws.on("close", () => {
      sockets.delete(ws); if (sockets.size) return; this.connections.delete(context.actorId);
      this.node.engine.n.centralModelRuntime.remove({ sessionId: context.actorId, requestId: `disconnect:${context.actorId}:${Date.now()}` }); this.rejectWorkerJobs(context.actorId, new Error("Browser worker disconnected.")); this.node.persist();
    });
  }

  handleWorkerMessage(context, message) {
    if (!context.scopes.includes("inference.contribute") && !context.scopes.includes("admin")) return;
    const active = this.workerJobs.get(message.jobId);
    if (!active || active.sessionId !== context.actorId) return;
    if (message.type === "worker.chunk") active.onChunk(String(message.content || ""));
    if (message.type === "worker.complete") { clearTimeout(active.timeout); this.workerJobs.delete(message.jobId); active.resolve(); }
    if (message.type === "worker.error") { clearTimeout(active.timeout); this.workerJobs.delete(message.jobId); active.reject(new Error(String(message.error || "Browser inference failed."))); }
  }

  dispatchBrowser(sessionId, offer, onChunk, signal) {
    const socket = [...(this.connections.get(sessionId) || [])].find((item) => item.readyState === item.OPEN);
    if (!socket || socket.readyState !== socket.OPEN) return Promise.reject(new Error("Browser worker is not connected."));
    return new Promise((resolvePromise, reject) => {
      const timeout = setTimeout(() => { this.workerJobs.delete(offer.jobId); reject(new Error("Browser inference timed out.")); }, 5 * 60_000);
      this.workerJobs.set(offer.jobId, { sessionId, onChunk, resolve: resolvePromise, reject, timeout });
      signal.addEventListener("abort", () => { socket.send(JSON.stringify({ type: "worker.cancel", jobId: offer.jobId })); clearTimeout(timeout); this.workerJobs.delete(offer.jobId); reject(new Error("Browser inference cancelled.")); }, { once: true });
      socket.send(JSON.stringify({ type: "worker.offer", data: offer, at: Date.now() }));
    });
  }

  rejectWorkerJobs(sessionId, error) {
    for (const [jobId, active] of this.workerJobs) if (active.sessionId === sessionId) { clearTimeout(active.timeout); this.workerJobs.delete(jobId); active.reject(error); }
  }

  broadcast(event) { const message = JSON.stringify(event); for (const sockets of this.connections.values()) for (const socket of sockets) if (socket.readyState === socket.OPEN) socket.send(message); }
  async close() { this.node.setCodexGateway(null); this.gatewayTokens.clear(); for (const sockets of this.connections.values()) for (const socket of sockets) socket.close(); await new Promise((resolvePromise) => this.server?.close(resolvePromise)); }
}
