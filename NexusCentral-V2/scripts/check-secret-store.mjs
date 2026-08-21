import assert from "node:assert/strict";
import { OsSecretStore } from "@nexuscentral/runtime-adapters";

if (process.platform !== "win32") {
  console.log(JSON.stringify({ status: "skipped", reason: "Windows DPAPI check runs only on win32." }, null, 2));
  process.exit(0);
}

const store = new OsSecretStore({ service: "NexusCentralV2-CI" });
const reference = `ci-${process.pid}-${Date.now()}`;
const secret = `nexuscentral-secret-${Date.now()}`;

try {
  assert.equal(store.set(reference, secret), reference);
  assert.equal(store.has(reference), true);
  assert.equal(store.get(reference), secret);
  assert.equal(store.delete(reference), true);
  assert.equal(store.has(reference), false);
  console.log(JSON.stringify({ status: "passed", platform: process.platform, provider: "Windows DPAPI CurrentUser" }, null, 2));
} finally {
  store.delete(reference);
}
