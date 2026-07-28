#!/usr/bin/env node
import { PeerRoomBroker } from "./peer-broker.mjs";

const tls = process.env.NEXUS_PEER_TLS_CERT && process.env.NEXUS_PEER_TLS_KEY ? { certPath: process.env.NEXUS_PEER_TLS_CERT, keyPath: process.env.NEXUS_PEER_TLS_KEY } : null;
const broker = new PeerRoomBroker({ host: process.env.NEXUS_PEER_HOST || "127.0.0.1", port: Number(process.env.NEXUS_PEER_PORT || 47900), path: process.env.NEXUS_PEER_PATH || "/peer", tls });
console.log(JSON.stringify({ status: "ready", ...(await broker.start()) }, null, 2));
const shutdown = async () => { await broker.close(); process.exit(0); };
process.on("SIGINT", shutdown); process.on("SIGTERM", shutdown);
