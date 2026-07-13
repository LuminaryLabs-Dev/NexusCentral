import { cpSync, mkdirSync, rmSync } from "node:fs";
import { resolve } from "node:path";
import { packager } from "@electron/packager";

const root = resolve(import.meta.dirname, "..");
const platform = process.argv[2] || process.platform;
const stage = resolve(root, ".package-stage", platform);
const payload = resolve(stage, "nexuscentral");
rmSync(stage, { recursive: true, force: true });
mkdirSync(payload, { recursive: true });

for (const source of ["adapters", "kits", "packages", "services", "node_modules", "package.json", "package-lock.json"]) cpSync(resolve(root, source), resolve(payload, source), { recursive: true });
cpSync(resolve(root, "apps/web/out"), resolve(payload, "apps/web/out"), { recursive: true });

const paths = await packager({
  dir: resolve(root, "apps/electron"),
  name: "NexusCentral",
  appVersion: "0.1.0",
  platform,
  arch: process.arch,
  out: resolve(root, "release"),
  overwrite: true,
  asar: true,
  extraResource: [payload],
  prune: false
});
console.log(JSON.stringify({ platform, paths }, null, 2));
