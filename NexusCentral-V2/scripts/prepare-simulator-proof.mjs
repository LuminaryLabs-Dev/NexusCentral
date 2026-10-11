import { cpSync, existsSync, mkdirSync, rmSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const root = resolve(here, "..");
const source = resolve(root, "apps/simulator-proof/source");
const destination = resolve(root, "apps/simulator-proof/dist");
const nexusEngineSource = resolve(root, "node_modules/nexusengine/src");

if (!existsSync(nexusEngineSource)) {
  throw new Error("Install the pinned NexusEngine dependency before preparing the proof.");
}

rmSync(destination, { recursive: true, force: true });
mkdirSync(destination, { recursive: true });
cpSync(source, destination, { recursive: true });
cpSync(resolve(root, "kits"), resolve(destination, "kits"), { recursive: true });
cpSync(resolve(root, "packages/composition"), resolve(destination, "packages/composition"), { recursive: true });
cpSync(resolve(root, "packages/workflows"), resolve(destination, "packages/workflows"), { recursive: true });
cpSync(nexusEngineSource, resolve(destination, "vendor/nexusengine"), { recursive: true });

console.log(`Prepared NexusSimulator browser proof at ${destination}`);
