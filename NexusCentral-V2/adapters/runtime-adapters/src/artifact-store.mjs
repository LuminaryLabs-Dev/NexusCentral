import { createHash } from "node:crypto";
import { appendFileSync, existsSync, mkdirSync, readFileSync, readdirSync, statSync, writeFileSync } from "node:fs";
import { dirname, extname, isAbsolute, join, relative, resolve, sep } from "node:path";

const MEDIA_TYPES = {
  ".json": "application/json", ".jsonl": "application/x-ndjson", ".log": "text/plain", ".md": "text/markdown",
  ".patch": "text/x-diff", ".txt": "text/plain", ".webm": "video/webm", ".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".zip": "application/zip"
};

export class ArtifactStoreAdapter {
  constructor({ root, maxReadBytes = 5 * 1024 * 1024 }) {
    this.root = resolve(root);
    this.maxReadBytes = maxReadBytes;
    mkdirSync(this.root, { recursive: true });
  }

  path(logicalPath) {
    const target = resolve(this.root, String(logicalPath));
    assertInside(this.root, target);
    return target;
  }

  logicalPath(absolutePath) {
    const target = resolve(absolutePath);
    assertInside(this.root, target);
    return relative(this.root, target);
  }

  writeText(logicalPath, content) {
    const target = this.path(logicalPath);
    mkdirSync(dirname(target), { recursive: true });
    writeFileSync(target, String(content));
    return target;
  }

  append(logicalPath, content) {
    const target = this.path(logicalPath);
    mkdirSync(dirname(target), { recursive: true });
    appendFileSync(target, content);
    return target;
  }

  writeJson(logicalPath, value) {
    return this.writeText(logicalPath, `${JSON.stringify(value, null, 2)}\n`);
  }

  recordFile(path, metadata = {}) {
    const target = resolve(path);
    assertInside(this.root, target);
    if (!existsSync(target) || !statSync(target).isFile()) throw new Error("Artifact file does not exist.");
    const logicalPath = relative(this.root, target);
    const bytes = readFileSync(target);
    return {
      id: artifactId(logicalPath), path: logicalPath, hash: createHash("sha256").update(bytes).digest("hex"), bytes: bytes.length,
      mediaType: MEDIA_TYPES[extname(target).toLowerCase()] || "application/octet-stream", kind: String(metadata.kind || inferKind(logicalPath)),
      jobId: String(metadata.jobId || metadata.runId || ""), runId: String(metadata.runId || metadata.jobId || ""), stage: String(metadata.stage || ""),
      provenance: metadata.provenance || {}, createdAt: Number(metadata.createdAt || Date.now())
    };
  }

  read(artifact) {
    const target = this.path(artifact.path);
    if (!existsSync(target) || !statSync(target).isFile()) throw new Error("Artifact bytes are unavailable.");
    const bytes = readFileSync(target);
    if (bytes.length > this.maxReadBytes) throw new Error("Artifact exceeds the command read limit. Use the download endpoint.");
    const hash = createHash("sha256").update(bytes).digest("hex");
    if (artifact.hash && hash !== artifact.hash) throw new Error("Artifact bytes no longer match the recorded hash.");
    const textual = /^(text\/|application\/(json|x-ndjson))/.test(artifact.mediaType);
    return { ...artifact, encoding: textual ? "utf8" : "base64", content: textual ? bytes.toString("utf8") : bytes.toString("base64") };
  }

  open(artifact) {
    const target = this.path(artifact.path);
    if (!existsSync(target) || !statSync(target).isFile()) throw new Error("Artifact bytes are unavailable.");
    return { path: target, size: statSync(target).size, mediaType: artifact.mediaType || "application/octet-stream" };
  }

  discoverRetained() {
    const files = [];
    const experiments = this.path("experiments");
    if (existsSync(experiments)) for (const runId of readdirSync(experiments)) {
      const runRoot = join(experiments, runId);
      for (const name of ["manifest.json", "events.jsonl", "validation-report.json"]) collectFile(join(runRoot, name), files);
      for (const name of ["inputs", "outputs", "decisions", "validation"]) collectTree(join(runRoot, name), files);
    }
    collectTree(this.path("runs"), files);
    return files.map((path) => this.recordFile(path, { runId: runIdFromPath(this.logicalPath(path)), provenance: { source: "startup-index" } }));
  }
}

function artifactId(path) { return `artifact:${createHash("sha256").update(path).digest("hex").slice(0, 24)}`; }
function inferKind(path) { return path.includes("validation") ? "validation" : path.endsWith(".patch") ? "diff" : path.includes("decisions") ? "decision" : path.includes("inputs") ? "input" : "output"; }
function runIdFromPath(path) { const parts = path.split(sep); return parts[0] === "experiments" || parts[0] === "runs" ? parts[1] || "" : ""; }
function collectFile(path, output) { if (existsSync(path) && statSync(path).isFile()) output.push(path); }
function collectTree(root, output) { if (!existsSync(root)) return; for (const entry of readdirSync(root, { withFileTypes: true })) entry.isDirectory() ? collectTree(join(root, entry.name), output) : entry.isFile() && output.push(join(root, entry.name)); }
function assertInside(root, target) { const rel = relative(resolve(root), resolve(target)); if (rel === ".." || rel.startsWith(`..${sep}`) || isAbsolute(rel)) throw new Error("Artifact path escapes the NexusCentral data root."); }
