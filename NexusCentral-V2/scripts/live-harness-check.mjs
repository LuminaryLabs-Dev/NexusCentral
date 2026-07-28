import assert from "node:assert/strict";
import { mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { resolve, join } from "node:path";
import { commandEnvelope } from "@nexuscentral/contracts";
import { MemorySecretStore } from "@nexuscentral/runtime-adapters";
import { NexusNode } from "../services/nexus-node/src/nexus-node.mjs";

const proofRoot = resolve("output/harness-live-proof");
const source = join(proofRoot, "source");
rmSync(proofRoot, { recursive: true, force: true }); mkdirSync(source, { recursive: true });
writeFileSync(join(source, "package.json"), `${JSON.stringify({ name: "nexuscentral-harness-fixture", private: true, type: "module", scripts: { check: "node validate.mjs" } }, null, 2)}\n`);
writeFileSync(join(source, "index.mjs"), "export function greet() { return 'hello'; }\n");
writeFileSync(join(source, "validate.mjs"), "import { greet } from './index.mjs'; if (greet('Nexus') !== 'Hello, Nexus!') throw new Error('greet contract failed');\n");
writeFileSync(join(source, "index.html"), "<!doctype html><meta charset='utf-8'><style>body{font:20px system-ui;padding:40px;background:#08251f;color:#e8f2ec}button{padding:12px}button:focus{transform:translateX(12px);background:#62d4ab}</style><h1>Harness proof</h1><button>Focus proof</button>\n");
const baseline = readFileSync(join(source, "index.mjs"), "utf8");

const node = new NexusNode({ databasePath: join(proofRoot, "state.sqlite"), secretStore: new MemorySecretStore() });
const admin = { actorId: "live-harness-proof", scopes: ["admin"] };
const command = (name, payload = {}) => node.execute(commandEnvelope(name, payload), admin);

async function waitFor(id) {
  for (let attempt = 0; attempt < 1200; attempt += 1) {
    const run = node.engine.n.centralExperiments.getSnapshot().experiments.find((item) => item.id === id);
    if (["review", "failed"].includes(run.status)) return run;
    await new Promise((resolvePromise) => setTimeout(resolvePromise, 500));
  }
  throw new Error(`Experiment ${id} timed out.`);
}

try {
  await command("workspace.open", { path: source, workspaceId: "live-harness-fixture" });
  const model = process.env.CODEX_TEST_MODEL || "gpt-5.4";
  await command("harnesses.select", { id: "review-harness" });
  await command("harnesses.configure", { id: "review-harness", model, sandbox: "read-only" });
  const review = await waitFor((await command("lab.submit", { task: "Inspect index.mjs and explain its current behavior in one concise sentence." })).id);
  assert.equal(review.status, "review", review.error);
  assert.equal(readFileSync(join(source, "index.mjs"), "utf8"), baseline);

  await command("harnesses.select", { id: "coding-harness" });
  await command("harnesses.configure", { id: "coding-harness", model, sandbox: "workspace-write", validationCommands: [{ command: "npm", args: ["run", "check"] }], simulatorTarget: "." });
  const coding = await waitFor((await command("lab.submit", { task: "Update greet so greet(name) returns exactly `Hello, ${name}!`. Keep the implementation minimal." })).id);
  assert.equal(coding.status, "review", coding.error);
  assert.equal(coding.validation.passed, true);
  assert.equal(coding.diff.empty, false);
  assert.equal(readFileSync(join(source, "index.mjs"), "utf8"), baseline, "Source fixture must remain unchanged before promotion.");
  const report = { status: "passed", sourceUntouched: true, codex: await node.harnessProcess.discover(), runs: [{ id: review.id, harness: review.harnessId, artifacts: review.artifacts }, { id: coding.id, harness: coding.harnessId, attempts: coding.attempt, diff: coding.diff, validation: coding.validation, artifacts: coding.artifacts }] };
  writeFileSync(join(proofRoot, "live-proof-report.json"), `${JSON.stringify(report, null, 2)}\n`); console.log(JSON.stringify(report, null, 2));
} finally { await node.close(); }
