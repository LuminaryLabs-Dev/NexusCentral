import { lstatSync, mkdirSync, readFileSync, readdirSync, realpathSync, writeFileSync } from "node:fs";
import { basename, dirname, isAbsolute, relative, resolve, sep } from "node:path";

function assertInside(root, target) {
  const rel = relative(root, target);
  if (rel === ".." || rel.startsWith(`..${sep}`) || isAbsolute(rel)) throw new Error("Workspace path escapes its root.");
  return target;
}

export class FilesystemWorkspaceAdapter {
  constructor() { this.roots = new Map(); }

  open(rootPath, workspaceId) {
    const canonical = realpathSync(rootPath);
    this.roots.set(workspaceId, canonical);
    return { workspaceId, name: canonical.split(sep).at(-1), canonicalPath: canonical, entries: this.list(workspaceId) };
  }

  create(rootPath, workspaceId) {
    mkdirSync(rootPath, { recursive: true });
    return this.open(rootPath, workspaceId);
  }

  resolve(workspaceId, logicalPath = ".") {
    const root = this.roots.get(workspaceId);
    if (!root) throw new Error("Workspace is not open on this host.");
    return assertInside(root, resolve(root, logicalPath));
  }

  list(workspaceId) {
    const root = this.resolve(workspaceId);
    return readdirSync(root, { withFileTypes: true }).slice(0, 500).map((entry) => ({ name: entry.name, kind: entry.isDirectory() ? "directory" : "file" }));
  }

  read(workspaceId, logicalPath) {
    const root = this.root(workspaceId);
    const target = this.resolve(workspaceId, logicalPath);
    if (lstatSync(target).isSymbolicLink()) throw new Error("Symbolic links are not readable through LAN access.");
    return readFileSync(assertInside(root, realpathSync(target)), "utf8");
  }

  write(workspaceId, logicalPath, content) {
    const root = this.root(workspaceId);
    const requested = this.resolve(workspaceId, logicalPath);
    const parent = secureDirectory(root, relative(root, dirname(requested)));
    const target = assertInside(root, resolve(parent, basename(requested)));
    if (lstatExists(target) && lstatSync(target).isSymbolicLink()) throw new Error("Symbolic links are not writable through LAN access.");
    writeFileSync(target, String(content), { encoding: "utf8", flag: "w" });
    return { logicalPath, bytes: Buffer.byteLength(String(content)) };
  }

  root(workspaceId) {
    const root = this.roots.get(workspaceId);
    if (!root) throw new Error("Workspace is not open on this host.");
    return root;
  }
}

function lstatExists(path) {
  try { lstatSync(path); return true; }
  catch (error) { if (error.code === "ENOENT") return false; throw error; }
}

function secureDirectory(root, logicalPath) {
  let current = root;
  for (const part of logicalPath.split(sep).filter(Boolean)) {
    const candidate = assertInside(root, resolve(current, part));
    if (!lstatExists(candidate)) mkdirSync(candidate);
    const metadata = lstatSync(candidate);
    if (metadata.isSymbolicLink() || !metadata.isDirectory()) throw new Error("Workspace directories must not contain symbolic links.");
    current = assertInside(root, realpathSync(candidate));
  }
  return current;
}
