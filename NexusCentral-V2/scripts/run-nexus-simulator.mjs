import { existsSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { spawnSync } from "node:child_process";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "..");
const simulatorCli = process.env.NEXUS_SIMULATOR_CLI
  ? resolve(process.env.NEXUS_SIMULATOR_CLI)
  : resolve(root, "../../NexusSimulator/NexusSimulator-V1/src/cli.js");
const mode = process.argv[2];

if (!existsSync(simulatorCli)) {
  throw new Error("NexusSimulator CLI was not found. Set NEXUS_SIMULATOR_CLI to its src/cli.js path.");
}

function run(args) {
  const result = spawnSync(process.execPath, [simulatorCli, ...args], {
    cwd: root,
    encoding: "utf8"
  });
  if (result.stdout) process.stdout.write(result.stdout);
  if (result.stderr) process.stderr.write(result.stderr);
  if (result.status !== 0) process.exit(result.status ?? 1);
}

function runNode(script, args = []) {
  const result = spawnSync(process.execPath, [script, ...args], {
    cwd: root,
    encoding: "utf8"
  });
  if (result.stdout) process.stdout.write(result.stdout);
  if (result.stderr) process.stderr.write(result.stderr);
  if (result.status !== 0) process.exit(result.status ?? 1);
}

if (mode === "contracts") {
  mkdirSync(resolve(root, "output"), { recursive: true });
  runNode(resolve(root, "scripts/prepare-contract-proof.mjs"));
  run([
    "tools", "run", "kit.contract-proof",
    "--input", resolve(root, "output/kit-contracts.jsonl"),
    "--output", resolve(root, "output/kit-contract-report.json"),
    "--run-id", "nexuscentral-v2-foundation"
  ]);
} else if (mode === "browser") {
  runNode(resolve(root, "scripts/prepare-simulator-proof.mjs"));
  run([
    "validate", resolve(root, "apps/simulator-proof/dist"),
    "--tool", "interaction.proof"
  ]);
} else {
  throw new Error("Usage: node scripts/run-nexus-simulator.mjs <contracts|browser>");
}
