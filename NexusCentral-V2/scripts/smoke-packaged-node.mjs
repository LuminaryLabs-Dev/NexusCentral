import assert from "node:assert/strict";
import { existsSync, mkdtempSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { spawn } from "node:child_process";
import { once } from "node:events";

const root = resolve(import.meta.dirname, "..");
const platform = process.argv[2] || process.platform;
const arch = process.argv[3] || process.arch;
const packageRoot = resolve(root, "release", `NexusCentral-${platform}-${arch}`);

function layout() {
  if (platform === "darwin") {
    return {
      executable: join(packageRoot, "NexusCentral.app", "Contents", "MacOS", "NexusCentral"),
      resources: join(packageRoot, "NexusCentral.app", "Contents", "Resources", "nexuscentral")
    };
  }
  return {
    executable: join(packageRoot, platform === "win32" ? "NexusCentral.exe" : "NexusCentral"),
    resources: join(packageRoot, "resources", "nexuscentral")
  };
}

const { executable, resources } = layout();
const serviceCli = join(resources, "services", "nexus-node", "src", "cli.mjs");
assert.equal(existsSync(executable), true, `Packaged executable is missing: ${executable}`);
assert.equal(existsSync(serviceCli), true, `Packaged Nexus Node CLI is missing: ${serviceCli}`);

const dataRoot = mkdtempSync(join(tmpdir(), "nexuscentral-package-smoke-"));
const port = 47981;
let stdout = "";
let stderr = "";
const child = spawn(executable, [serviceCli], {
  cwd: resources,
  env: {
    ...process.env,
    ELECTRON_RUN_AS_NODE: "1",
    NEXUS_CENTRAL_DATA: dataRoot,
    NEXUS_CENTRAL_HOST: "127.0.0.1",
    NEXUS_CENTRAL_PORT: String(port)
  },
  stdio: ["ignore", "pipe", "pipe"]
});
child.stdout.on("data", (chunk) => { stdout += chunk.toString(); });
child.stderr.on("data", (chunk) => { stderr += chunk.toString(); });
const exitPromise = once(child, "exit");

async function waitForCapabilities() {
  const deadline = Date.now() + 30000;
  while (Date.now() < deadline) {
    if (child.exitCode !== null) throw new Error(`Packaged Nexus Node exited early (${child.exitCode}).\n${stderr || stdout}`);
    try {
      const response = await fetch(`http://127.0.0.1:${port}/api/v1/capabilities`, { signal: AbortSignal.timeout(1000) });
      if (response.ok) return response.json();
    } catch (_error) {}
    await new Promise((resolvePromise) => setTimeout(resolvePromise, 250));
  }
  throw new Error(`Packaged Nexus Node did not become ready.\n${stderr || stdout}`);
}

try {
  const capabilities = await waitForCapabilities();
  assert.ok(capabilities && typeof capabilities === "object");
  console.log(JSON.stringify({ status: "passed", platform, arch, executable, serviceCli }, null, 2));
} finally {
  if (child.exitCode === null) {
    child.kill("SIGTERM");
    await Promise.race([exitPromise, new Promise((resolvePromise) => setTimeout(resolvePromise, 5000))]);
    if (child.exitCode === null) child.kill("SIGKILL");
  }
  rmSync(dataRoot, { recursive: true, force: true });
}
