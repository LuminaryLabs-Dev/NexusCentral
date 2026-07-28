#!/usr/bin/env node
import { existsSync, mkdirSync } from "node:fs";
import { homedir } from "node:os";
import { join, resolve } from "node:path";
import { spawnSync } from "node:child_process";
import { NexusNode } from "./nexus-node.mjs";
import { LanServer } from "./lan-server.mjs";

const args = new Set(process.argv.slice(2));
const root = resolve(import.meta.dirname, "../../..");
const dataRoot = process.env.NEXUS_CENTRAL_DATA || join(homedir(), ".nexuscentral-v2");
const lan = args.has("--lan");
const host = process.env.NEXUS_CENTRAL_HOST || (lan ? "0.0.0.0" : "127.0.0.1");
const port = Number(process.env.NEXUS_CENTRAL_PORT || 47800);
mkdirSync(dataRoot, { recursive: true });

function tlsConfig() {
  if (!lan && !process.env.NEXUS_CENTRAL_TLS_CERT) return null;
  const certPath = process.env.NEXUS_CENTRAL_TLS_CERT || join(dataRoot, "tls", "cert.pem");
  const keyPath = process.env.NEXUS_CENTRAL_TLS_KEY || join(dataRoot, "tls", "key.pem");
  if (!existsSync(certPath) || !existsSync(keyPath)) {
    mkdirSync(join(dataRoot, "tls"), { recursive: true });
    const result = spawnSync("openssl", ["req", "-x509", "-newkey", "rsa:2048", "-nodes", "-keyout", keyPath, "-out", certPath, "-days", "30", "-subj", "/CN=NexusCentral.local"], { stdio: "ignore" });
    if (result.status !== 0) throw new Error("LAN mode requires TLS certificates. Set NEXUS_CENTRAL_TLS_CERT and NEXUS_CENTRAL_TLS_KEY.");
  }
  return { certPath, keyPath };
}

const node = new NexusNode({ databasePath: join(dataRoot, "nexuscentral.sqlite") });
const server = new LanServer({ node, staticRoot: join(root, "apps", "web", "out"), host, port, tls: tlsConfig() });
const address = await server.start();
console.log(JSON.stringify({ status: "ready", ...address, nodeId: node.engine.n.centralNode.getSnapshot().nodeId }, null, 2));

const shutdown = async () => { await server.close(); await node.close(); process.exit(0); };
process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
