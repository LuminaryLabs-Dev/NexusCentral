import { createHash } from "node:crypto";
import { execFileSync } from "node:child_process";
import { existsSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { join } from "node:path";

export class MemorySecretStore {
  constructor() { this.values = new Map(); }
  set(reference, value) { this.values.set(reference, String(value)); return reference; }
  get(reference) { return this.values.get(reference) || ""; }
  has(reference) { return this.values.has(reference); }
  delete(reference) { return this.values.delete(reference); }
}

export class OsSecretStore {
  constructor({ service = "NexusCentralV2", windowsRoot } = {}) {
    this.service = service;
    this.windowsRoot = windowsRoot || join(process.env.LOCALAPPDATA || process.env.APPDATA || homedir(), "NexusCentral", "secrets");
  }

  windowsPath(reference) {
    const name = createHash("sha256").update(`${this.service}\0${reference}`).digest("hex");
    return join(this.windowsRoot, `${name}.dpapi`);
  }

  powershell(script, input) {
    return execFileSync("powershell.exe", ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", script], {
      input,
      encoding: "utf8",
      stdio: ["pipe", "pipe", "ignore"]
    }).trim();
  }

  set(reference, value) {
    if (process.platform === "darwin") {
      execFileSync("security", ["add-generic-password", "-U", "-s", this.service, "-a", reference, "-w", String(value)], { stdio: "ignore" });
      return reference;
    }
    if (process.platform === "linux") {
      execFileSync("secret-tool", ["store", "--label", this.service, "service", this.service, "account", reference], { input: String(value), stdio: ["pipe", "ignore", "ignore"] });
      return reference;
    }
    if (process.platform === "win32") {
      mkdirSync(this.windowsRoot, { recursive: true });
      const encrypted = this.powershell(
        "Add-Type -AssemblyName System.Security;$plain=[Console]::In.ReadToEnd();$bytes=[Text.Encoding]::UTF8.GetBytes($plain);$protected=[Security.Cryptography.ProtectedData]::Protect($bytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser);[Convert]::ToBase64String($protected)",
        String(value)
      );
      writeFileSync(this.windowsPath(reference), encrypted, { encoding: "utf8", mode: 0o600 });
      return reference;
    }
    throw new Error("OS secret storage is unavailable on this platform. Configure a supported secure-storage adapter.");
  }

  get(reference) {
    try {
      if (process.platform === "darwin") return execFileSync("security", ["find-generic-password", "-s", this.service, "-a", reference, "-w"], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
      if (process.platform === "linux") return execFileSync("secret-tool", ["lookup", "service", this.service, "account", reference], { encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] }).trim();
      if (process.platform === "win32") {
        const path = this.windowsPath(reference);
        if (!existsSync(path)) return "";
        const encrypted = readFileSync(path, "utf8").trim();
        if (!encrypted) return "";
        return this.powershell(
          "Add-Type -AssemblyName System.Security;$encoded=[Console]::In.ReadToEnd().Trim();$bytes=[Convert]::FromBase64String($encoded);$plain=[Security.Cryptography.ProtectedData]::Unprotect($bytes,$null,[Security.Cryptography.DataProtectionScope]::CurrentUser);[Text.Encoding]::UTF8.GetString($plain)",
          encrypted
        );
      }
    } catch (_error) { return ""; }
    return "";
  }

  has(reference) { return Boolean(this.get(reference)); }

  delete(reference) {
    try {
      if (process.platform === "darwin") execFileSync("security", ["delete-generic-password", "-s", this.service, "-a", reference], { stdio: "ignore" });
      else if (process.platform === "linux") execFileSync("secret-tool", ["clear", "service", this.service, "account", reference], { stdio: "ignore" });
      else if (process.platform === "win32") {
        const path = this.windowsPath(reference);
        if (!existsSync(path)) return false;
        rmSync(path, { force: true });
      } else return false;
      return true;
    } catch (_error) { return false; }
  }
}
