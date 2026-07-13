import { mkdirSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { createCentralRuntime } from "../packages/composition/index.mjs";

const engine = createCentralRuntime({ node: { nodeId: "simulator-contract-node" } });
const requires = {
  "n:central": ["n:realtime"], "n:central:node": ["n:central"], "n:central:audit": ["n:central:node"], "n:central:policy": ["n:central:node", "n:central:audit"],
  "n:central:workspace": ["n:central"], "n:central:artifacts": ["n:central:workspace", "n:central:audit"], "n:central:providers": ["n:central", "n:central:audit"],
  "n:central:model-catalog": ["n:central", "n:central:audit"], "n:central:model-runtime": ["n:central:model-catalog", "n:central:node", "n:central:audit"],
  "n:central:mesh-membership": ["n:central:node", "n:central:audit"], "n:central:routing": ["n:central:providers", "n:central:model-runtime", "n:central:mesh-membership", "n:central:policy"],
  "n:central:harnesses": ["n:central"], "n:central:jobs": ["n:central:routing", "n:central:policy", "n:central:audit"], "n:central:inference": ["n:central:jobs", "n:central:model-runtime"],
  "n:central:workspace-transfer": ["n:central:workspace", "n:central:artifacts", "n:central:jobs", "n:central:policy"], "n:central:proof": ["n:central:jobs", "n:central:artifacts", "n:central:audit"],
  "n:central:lab": ["n:central:workspace", "n:central:harnesses", "n:central:jobs", "n:central:proof"],
  "n:central:experiments": ["n:central:harnesses", "n:central:workspace", "n:central:jobs", "n:central:proof", "n:central:audit"],
  "n:central:lan-access": ["n:central:node", "n:central:policy", "n:central:audit"]
};

const records = engine.n.paths().filter((entry) => entry.path.startsWith("n:central")).map((entry) => {
  const api = engine.n[entry.metadata.apiName];
  const snapshot = api.getSnapshot();
  const methods = Object.keys(api).filter((name) => !["getSnapshot", "loadSnapshot", "reset"].includes(name));
  return {
    record_id: entry.ownerKitId,
    payload: {
      atomic: true, idempotent: true, owned_state: Object.keys(snapshot), inputs: [...methods, "load-snapshot", "reset"], outputs: ["changed"],
      requires: requires[entry.path], provides: [entry.path, `${entry.path}:changed`], idempotency_key: "processedRequestIds",
      snapshot: { fields: Object.keys(snapshot), supportsSnapshot: true, supportsLoadSnapshot: true, supportsReset: true },
      reset_behavior: `Reset ${Object.keys(snapshot).join(" ")} without host side effects.`, renderer_boundary: { ownsDom: false, ownsCanvas: false, ownsThreeObjects: false },
      tests: ["metadata", "dependencies", ...methods, "idempotency", "snapshot", "load snapshot", "reset", "invalid input", "renderer isolation"]
    }
  };
});

mkdirSync(resolve("output"), { recursive: true });
const destination = resolve("output/kit-contracts.jsonl");
writeFileSync(destination, `${records.map((record) => JSON.stringify(record)).join("\n")}\n`);
console.log(`Prepared ${records.length} live kit descriptors at ${destination}`);
