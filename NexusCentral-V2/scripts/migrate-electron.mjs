import { existsSync, mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { join, resolve } from "node:path";
import { MemorySecretStore } from "@nexuscentral/runtime-adapters";
import { commandEnvelope } from "@nexuscentral/contracts";
import { NexusNode } from "../services/nexus-node/src/nexus-node.mjs";

const args = process.argv.slice(2);
const value = (name, fallback) => { const index = args.indexOf(name); return index >= 0 ? args[index + 1] : fallback; };
const source = resolve(value("--source", join(homedir(), "Library/Application Support/nexuscentral-electron")));
const target = resolve(value("--target", join(homedir(), ".nexuscentral-v2")));
const apply = args.includes("--apply");
const report = { source, target, apply, imported: [], skipped: [], warnings: [] };

function readJson(path) { try { return JSON.parse(readFileSync(path, "utf8")); } catch (_error) { return null; } }

const registryPath = join(source, "router", "registry.json");
const registry = readJson(registryPath);
if (registry?.providers?.length) report.imported.push({ kind: "provider-profiles", count: registry.providers.length });
else report.skipped.push({ kind: "provider-profiles", reason: registry ? "Registry contains no provider profiles." : "No readable registry." });
const legacyHarnessId = registry?.version === 1 && registry.config?.endpointType === "codex-cli" ? "coding-harness" : null;
if (legacyHarnessId) report.imported.push({ kind: "selected-harness", id: legacyHarnessId });

const labState = readJson(join(source, "lab-loop", "latest.json"));
if (labState) report.imported.push({ kind: "lab-latest", count: 1 });
else report.skipped.push({ kind: "lab-latest", reason: "No readable Lab state." });

if (existsSync(join(source, "mesh", "identity.json"))) report.warnings.push("Legacy mesh private keys are not copied. V2 creates a libp2p identity directly in OS secure storage.");
if (existsSync(join(source, "router", "secrets.json"))) report.warnings.push("Encrypted provider credentials require migration from the original Electron safeStorage context and were not exported.");

if (apply) {
  mkdirSync(target, { recursive: true });
  const node = new NexusNode({ databasePath: join(target, "nexuscentral.sqlite"), secretStore: new MemorySecretStore() });
  for (const profile of registry?.providers || []) {
    await node.execute(commandEnvelope("providers.save", { id: profile.id, kind: profile.providerType, name: profile.name, baseUrl: profile.baseUrl, modelId: profile.modelId }), { actorId: "migration", scopes: ["admin"] });
  }
  if (legacyHarnessId) await node.execute(commandEnvelope("harnesses.select", { id: legacyHarnessId }), { actorId: "migration", scopes: ["admin"] });
  if (labState) {
    const id = `legacy:${labState.runId || "latest"}`;
    node.engine.n.centralProof.record({ id, jobId: String(labState.runId || "legacy"), assertions: [{ name: "legacy-import", passed: Boolean(labState.ok) }], artifacts: [], status: labState.ok ? "passed" : "attention", createdAt: Date.now(), requestId: id });
    node.persist();
  }
  await node.close();
}

mkdirSync(target, { recursive: true });
const reportPath = join(target, "migration-report.json");
writeFileSync(reportPath, JSON.stringify(report, null, 2));
console.log(JSON.stringify({ ...report, reportPath }, null, 2));
