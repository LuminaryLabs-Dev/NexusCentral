import { spawnSync } from "node:child_process";
import { existsSync, mkdirSync, rmSync, writeFileSync } from "node:fs";
import { homedir } from "node:os";
import { dirname, join, resolve } from "node:path";

const LABEL = "dev.luminarylabs.nexuscentral-v2";

export class MacOsLaunchAgentAdapter {
  constructor({ projectRoot, nodeExecutable = process.execPath, label = LABEL } = {}) {
    this.projectRoot = resolve(projectRoot || process.cwd());
    this.nodeExecutable = resolve(nodeExecutable);
    this.label = label;
    this.plistPath = join(homedir(), "Library", "LaunchAgents", `${label}.plist`);
  }

  status() {
    if (process.platform !== "darwin") return { supported: false, installed: false, running: false, label: this.label };
    const result = launchctl(["print", `gui/${process.getuid()}/${this.label}`], false);
    return { supported: true, installed: existsSync(this.plistPath), running: result.status === 0, label: this.label, plistPath: this.plistPath };
  }

  install() {
    this.#requireMac();
    const cliPath = join(this.projectRoot, "services", "nexus-node", "src", "cli.mjs");
    if (!existsSync(cliPath)) throw new Error("Nexus Node CLI was not found.");
    const dataRoot = join(homedir(), ".nexuscentral-v2");
    const logRoot = join(dataRoot, "logs");
    mkdirSync(dirname(this.plistPath), { recursive: true }); mkdirSync(logRoot, { recursive: true });
    writeFileSync(this.plistPath, plist({ label: this.label, nodeExecutable: this.nodeExecutable, cliPath, projectRoot: this.projectRoot, dataRoot, logRoot }));
    launchctl(["bootout", `gui/${process.getuid()}`, this.plistPath], false);
    launchctl(["bootstrap", `gui/${process.getuid()}`, this.plistPath]);
    launchctl(["enable", `gui/${process.getuid()}/${this.label}`]);
    launchctl(["kickstart", "-k", `gui/${process.getuid()}/${this.label}`]);
    return this.status();
  }

  uninstall() {
    this.#requireMac();
    launchctl(["bootout", `gui/${process.getuid()}`, this.plistPath], false);
    rmSync(this.plistPath, { force: true });
    return this.status();
  }

  #requireMac() { if (process.platform !== "darwin") throw new Error("LaunchAgent service control is available only on macOS."); }
}

function launchctl(args, required = true) {
  const result = spawnSync("/bin/launchctl", args, { encoding: "utf8" });
  if (required && result.status !== 0) throw new Error(result.stderr?.trim() || `launchctl ${args[0]} failed.`);
  return result;
}

function plist({ label, nodeExecutable, cliPath, projectRoot, dataRoot, logRoot }) {
  const item = (value) => `<string>${escapeXml(value)}</string>`;
  return `<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>Label</key>${item(label)}
<key>ProgramArguments</key><array>${item(nodeExecutable)}${item(cliPath)}</array>
<key>WorkingDirectory</key>${item(projectRoot)}
<key>EnvironmentVariables</key><dict><key>ELECTRON_RUN_AS_NODE</key><string>1</string><key>NEXUS_CENTRAL_DATA</key>${item(dataRoot)}<key>NEXUS_CENTRAL_HOST</key><string>127.0.0.1</string></dict>
<key>RunAtLoad</key><true/><key>KeepAlive</key><true/><key>ProcessType</key><string>Background</string>
<key>StandardOutPath</key>${item(join(logRoot, "stdout.log"))}<key>StandardErrorPath</key>${item(join(logRoot, "stderr.log"))}
</dict></plist>
`;
}

function escapeXml(value) { return String(value).replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&apos;"); }
