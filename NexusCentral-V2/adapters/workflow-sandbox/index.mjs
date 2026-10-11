import { spawn, spawnSync } from 'node:child_process';
import { mkdirSync, writeFileSync, readFileSync, lstatSync, realpathSync, rmSync } from 'node:fs';
import { join, resolve } from 'node:path';
const worker = join(import.meta.dirname, 'worker.mjs');
const launcher = join(import.meta.dirname, 'lockdown.py');
export class WorkflowSandbox {
  constructor(root) { this.root = resolve(root); mkdirSync(this.root, { recursive: true, mode: 0o700 }); this.root = realpathSync(this.root); }
  capability() {
    if (!this.cached) {
      const result = process.platform === 'linux' ? spawnSync('python3', [launcher, process.execPath, '-e', 'process.stdout.write("ready")'], { timeout: 5000, encoding: 'utf8', env: { PATH: process.env.PATH } }) : null;
      this.cached = { available: result?.status === 0 && result.stdout === 'ready', kind: 'trusted-builtin-process', network: 'denied', externalAgents: false, reason: 'Workflow workers require Linux, Python 3, libseccomp, and Node 24+. External agent isolation is not yet enabled.' };
    }
    return this.cached;
  }
  cleanup(runId) {
    if (!/^[a-zA-Z0-9_-]+$/.test(runId)) throw new Error('Invalid sandbox identity.');
    const root = join(this.root, runId);
    try { if (lstatSync(root).isSymbolicLink() || realpathSync(root) !== root) throw new Error('Sandbox links are forbidden.'); }
    catch (error) { if (error.code === 'ENOENT') return; throw error; }
    rmSync(root, { recursive: true });
  }
  async execute({ runId, blockId, attempt, action, config, inputs, signal }) {
    if (!this.capability().available) throw new Error(this.capability().reason);
    for (const part of [runId, blockId, String(attempt)]) if (!/^[a-zA-Z0-9_-]+$/.test(part)) throw new Error('Invalid sandbox identity.');
    const runRoot = join(this.root, runId); const root = join(runRoot, `${blockId}-${attempt}`);
    for (const dir of [runRoot, root]) { mkdirSync(dir, { recursive: true, mode: 0o700 }); if (lstatSync(dir).isSymbolicLink() || realpathSync(dir) !== dir) throw new Error('Sandbox links are forbidden.'); }
    writeFileSync(join(root, 'input.json'), JSON.stringify({ action, config, inputs }), { mode: 0o600 });
    const args = [launcher, process.execPath, '--permission', `--allow-fs-read=${root}`, `--allow-fs-read=${worker}`, `--allow-fs-write=${root}`, '--max-old-space-size=128', '--disable-proto=throw', '--disallow-code-generation-from-strings', worker];
    await new Promise((done, reject) => {
      if (signal?.aborted) return reject(new Error('Canceled.'));
      const child = spawn('python3', args, { cwd: root, env: { PATH: process.env.PATH, HOME: root, TMPDIR: root }, stdio: ['ignore', 'ignore', 'pipe'] });
      let errors = '', settled = false;
      const kill = () => child.kill('SIGKILL');
      const timeout = setTimeout(kill, 25000);
      signal?.addEventListener('abort', kill, { once: true });
      const finish = error => { if (settled) return; settled = true; clearTimeout(timeout); signal?.removeEventListener('abort', kill); error ? reject(error) : done(); };
      child.stderr.on('data', part => { errors += part.toString(); if (errors.length > 16000) kill(); });
      child.on('error', finish);
      child.on('close', code => finish(code === 0 ? null : new Error(signal?.aborted ? 'Canceled.' : errors.slice(0,1000) || 'Action stopped or exceeded its time limit.')));
    });
    const result = join(root, 'result.json');
    if (lstatSync(result).isSymbolicLink() || lstatSync(result).size > 4 * 1024 * 1024) throw new Error('Invalid sandbox output.');
    return JSON.parse(readFileSync(result, 'utf8')).output;
  }
}
