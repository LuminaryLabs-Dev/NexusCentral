import { execFileSync } from "node:child_process";

export class MemorySecretStore {
  constructor() { this.values = new Map(); }
  set(reference, value) { this.values.set(reference, String(value)); return reference; }
  get(reference) { return this.values.get(reference) || ""; }
  has(reference) { return this.values.has(reference); }
  delete(reference) { return this.values.delete(reference); }
}

export class OsSecretStore {
  constructor({ service = "NexusCentralV2" } = {}) { this.service = service; }

  set(reference, value) {
    if (process.platform === "darwin") {
      execFileSync("security", ["add-generic-password", "-U", "-s", this.service, "-a", reference, "-w", String(value)], { stdio: "ignore" });
      return reference;
    }
    if (process.platform === "linux") {
      execFileSync("secret-tool", ["store", "--label", this.service, "service", this.service, "account", reference], { input: String(value), stdio: ["pipe", "ignore", "ignore"] });
      return reference;
    }
    throw new Error("OS secret storage is unavailable on this platform. Configure a supported secure-storage adapter.");
  }

  get(reference) {
    try {
      if (process.platform === "darwin") return execFileSync("security", ["find-generic-password", "-s", this.service, "-a", reference, "-w"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
      if (process.platform === "linux") return execFileSync("secret-tool", ["lookup", "service", this.service, "account", reference], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
    } catch (_error) { return ""; }
    return "";
  }

  has(reference) { return Boolean(this.get(reference)); }
  delete(reference) {
    try {
      if (process.platform === "darwin") execFileSync("security", ["delete-generic-password", "-s", this.service, "-a", reference], { stdio: "ignore" });
      else if (process.platform === "linux") execFileSync("secret-tool", ["clear", "service", this.service, "account", reference], { stdio: "ignore" });
      else return false;
      return true;
    } catch (_error) { return false; }
  }
}
