import { randomUUID } from 'node:crypto';
import { join } from 'node:path';
import { ACTIONS, APP_CATALOG, actionFor, validateWorkflow } from '../../../../packages/workflows/catalog.mjs';
import { WorkflowSandbox } from '../../../../adapters/workflow-sandbox/index.mjs';

export class WorkflowCoordinator {
  constructor(node, sandbox = new WorkflowSandbox(join(node.dataRoot, 'workflow-sandboxes'))) {
    this.node = node; this.sandbox = sandbox; this.active = new Map(); this.closing = false;
    for (const run of this.snapshot().runs.filter(r => r.status === 'running')) {
      run.status = 'paused'; run.message = 'The service restarted. Resume from the last completed action.';
      for (const step of Object.values(run.steps)) if (step.status === 'running') step.status = 'pending';
      this.syncJob(run); this.saveRun(run);
    }
  }
  snapshot() { return this.node.engine.n.centralWorkflows.getSnapshot(); }
  state() { const snapshot = this.snapshot(); return { ...snapshot, actions: ACTIONS, catalog: APP_CATALOG, sandbox: this.sandbox.capability() }; }
  get(id) { const run = this.snapshot().runs.find(r => r.id === id); if (!run) throw new Error('Workflow run not found.'); return run; }
  receipt(run, tool, details = {}) { this.node.engine.n.centralAudit.append({ actorId: run.actorId, command: `workflow.tool.${tool}`, runId: run.id, at: Date.now(), ...details }); }
  saveRun(run) {
    run.updatedAt = Date.now(); this.node.engine.n.centralWorkflows.putRun({ run });
    this.node.persist();
    const event = { id: randomUUID(), type: 'workflow.changed', at: Date.now(), data: { runId: run.id, status: run.status } };
    this.node.store.appendEvent(event); this.node.emit('event', event);
    return run;
  }
  syncJob(run) {
    const jobs = this.node.engine.n.centralJobs;
    const status = ({ 'needs-review': 'approval_required', paused: 'paused' })[run.status] || run.status;
    jobs.finish({ id: run.id, status, error: run.error || null, result: { workflowId: run.definition.id, completed: Object.values(run.steps).filter(s => s.status === 'completed').length }, at: Date.now() });
  }
  async command(command, payload, context) {
    const read = command === 'workflows.list';
    this.node.requireScope(context, read ? 'view' : command === 'workflows.approve' || command === 'apps.configure' ? 'admin' : 'workspace.operate');
    if (read) return this.state();
    if (command === 'workflows.save') return this.node.engine.n.centralWorkflows.save(payload);
    if (command === 'workflows.remove') return this.node.engine.n.centralWorkflows.remove(payload);
    if (command === 'apps.configure') return this.node.engine.n.centralWorkflows.configureApp(payload);
    if (command === 'workflows.start') return this.start(payload, context);
    const run = this.get(payload.id);
    if (command === 'workflows.clean') {
      if (!['completed','canceled'].includes(run.status) || this.active.has(run.id)) throw new Error('Only finished, inactive sandboxes can be cleared.');
      this.sandbox.cleanup(run.id); run.sandboxClearedAt = Date.now(); this.receipt(run, 'sandbox.cleanup');
    } else if (command === 'workflows.pause') {
      if (run.status !== 'running') throw new Error('Only running workflows can be paused.');
      run.status = 'paused'; run.message = 'Paused. Active actions may finish; no next action will start.';
    } else if (command === 'workflows.cancel') {
      if (!['running','paused','needs-review','failed'].includes(run.status)) return run;
      run.status = 'canceled'; this.active.get(run.id)?.controller.abort();
    } else if (command === 'workflows.resume') {
      if (!['paused', 'failed'].includes(run.status)) throw new Error('This run cannot be resumed.');
      if (this.active.has(run.id)) throw new Error('Wait for the active action to finish before resuming.');
      if (this.snapshot().runs.some(r => r.id !== run.id && r.definition.id === run.definition.id && ['running', 'paused', 'needs-review'].includes(r.status))) throw new Error('This workflow already has an active run. Finish or stop it first.');
      if (this.active.size >= 3) throw new Error('Three workflows are already active. Wait for capacity.');
      for (const step of Object.values(run.steps)) if (['failed', 'running'].includes(step.status)) { step.status = 'pending'; step.error = null; }
      run.status = 'running'; run.error = null; run.message = ''; this.saveRun(run); this.syncJob(run); this.launch(run.id); return run;
    } else if (command === 'workflows.approve') {
      const step = run.steps[payload.blockId];
      if (run.status !== 'needs-review' || step?.status !== 'needs-review') throw new Error('This result is not awaiting review.');
      if (this.active.size >= 3) throw new Error('Three workflows are already active. Wait for capacity.');
      step.status = 'completed'; step.approvedBy = context.actorId; step.approvedAt = Date.now();
      this.receipt(run, 'review.approve', { blockId: payload.blockId });
      run.status = 'running'; this.saveRun(run); this.syncJob(run); this.launch(run.id); return run;
    } else throw new Error(`Unknown workflow command: ${command}`);
    this.syncJob(run); return this.saveRun(run);
  }
  start(payload, context) {
    const prior = this.snapshot().runs.find(r => r.requestId === payload.requestId); if (prior) return prior;
    const definition = this.snapshot().definitions.find(d => d.id === payload.id);
    const errors = validateWorkflow(definition, { runnable: true }); if (errors.length) throw new Error(errors.join(' '));
    if (!this.sandbox.capability().available) throw new Error(this.sandbox.capability().reason);
    if (this.snapshot().runs.some(r => r.definition.id === definition.id && ['running', 'paused', 'needs-review'].includes(r.status))) throw new Error('This workflow already has an active run. Finish or stop it first.');
    if (this.active.size >= 3) throw new Error('Three workflows are already active. Wait for capacity.');
    const disabled = definition.blocks.find(b => !this.snapshot().apps.some(app => app.id === actionFor(b).app && app.enabled));
    if (disabled) throw new Error('Connect the app used by this workflow first.');
    const run = { id: randomUUID(), requestId: payload.requestId, definition: structuredClone(definition), actorId: context.actorId, status: 'running', createdAt: Date.now(), steps: Object.fromEntries(definition.blocks.map(b => [b.id, { status: 'pending', attempts: 0 }])), sandbox: this.sandbox.capability(), actionVersions: definition.blocks.map(b => ({ id: b.action, version: b.version })) };
    this.node.engine.n.centralJobs.enqueue({ id: run.id, idempotencyKey: `workflow:${payload.requestId}`, kind: 'workflow', createdAt: run.createdAt, payload: { workflowId: definition.id } });
    this.node.engine.n.centralJobs.claim({ id: run.id, ownerId: 'workflow-coordinator', route: { kind: 'workflow' }, expiresAt: Date.now() + 3600000 });
    this.receipt(run, 'input.import', { source: 'workflow-configuration', destination: run.id });
    this.saveRun(run); this.launch(run.id); return run;
  }
  launch(id) {
    if (this.active.has(id) || this.closing) return;
    const controller = new AbortController();
    const promise = Promise.resolve().then(() => this.drive(id, controller.signal)).catch(error => {
      const run = this.get(id); if (!['paused', 'canceled'].includes(run.status)) { run.status = 'failed'; run.error = error.message; }
      this.syncJob(run); this.saveRun(run);
    }).finally(() => this.active.delete(id));
    this.active.set(id, { controller, promise });
  }
  async drive(id, signal) {
    while (!signal.aborted && !this.closing) {
      let run = this.get(id); if (run.status !== 'running') return;
      const pending = run.definition.blocks.filter(b => run.steps[b.id].status === 'pending');
      const ready = pending.filter(b => run.definition.edges.filter(e => e.to === b.id).every(e => ['completed','skipped'].includes(run.steps[e.from].status)));
      if (!ready.length) {
        if (Object.values(run.steps).some(s => s.status === 'needs-review')) run.status = 'needs-review';
        else if (Object.values(run.steps).some(s => s.status === 'failed')) run.status = 'failed';
        else if (pending.length) { run.status = 'failed'; run.error = 'No action can advance. Check the connections.'; }
        else run.status = 'completed';
        this.syncJob(run); this.saveRun(run); return;
      }
      await Promise.all(ready.slice(0, 3).map(block => this.executeStep(id, block, signal)));
      run = this.get(id);
      if (Object.values(run.steps).some(s => s.status === 'failed')) { run.status = 'failed'; run.error = 'An action could not finish. Review its details and retry.'; this.syncJob(run); this.saveRun(run); return; }
    }
  }
  async executeStep(id, block, signal) {
    let run = this.get(id); if (run.status !== 'running') return;
    const incoming = run.definition.edges.filter(e => e.to === block.id);
    const passing = incoming.filter(e => run.steps[e.from].status === 'completed' && (!e.when || e.when === 'always' || (e.when === 'has-items' ? run.steps[e.from].output?.length > 0 : run.steps[e.from].output?.length === 0)));
    if (incoming.length && !passing.length) { run.steps[block.id].status = 'skipped'; this.saveRun(run); return; }
    const inputs = passing.map(e => run.steps[e.from].output);
    if (block.action === 'review') { run.steps[block.id] = { ...run.steps[block.id], status: 'needs-review', output: inputs.join('\n\n') }; this.receipt(run, 'review.request', { blockId: block.id }); this.saveRun(run); return; }
    run.steps[block.id].status = 'running'; run.steps[block.id].attempts += 1; const attempt = run.steps[block.id].attempts; this.saveRun(run);
    try {
      const output = await this.sandbox.execute({ runId: id, blockId: block.id, attempt, action: block.action, config: block.config, inputs, signal });
      run = this.get(id); if (run.status === 'canceled' || this.closing) return;
      let artifact = null;
      if (typeof output === 'string') {
        const path = this.node.artifactStore.writeText(`runs/${id}/outputs/${block.id}.md`, output);
        artifact = this.node.artifactStore.recordFile(path, { runId: id, stage: block.id, provenance: { workflowId: run.definition.id, revision: run.definition.revision, actionVersion: block.version } });
        this.node.engine.n.centralArtifacts.record(artifact);
        this.receipt(run, 'artifact.write', { blockId: block.id, artifactId: artifact.id, hash: artifact.hash });
      }
      run.steps[block.id] = { ...run.steps[block.id], status: 'completed', output, artifact, completedAt: Date.now() };
      this.receipt(run, 'result.read', { blockId: block.id, source: id, destination: 'workflow-state' });
      this.saveRun(run);
    } catch (error) {
      run = this.get(id); if (run.status === 'canceled' || this.closing) return;
      run.steps[block.id] = { ...run.steps[block.id], status: 'failed', error: error.message }; this.saveRun(run);
    }
  }
  async close() {
    this.closing = true;
    for (const [id, active] of this.active) { const run = this.get(id); if (run.status === 'running') { run.status = 'paused'; this.syncJob(run); this.saveRun(run); } active.controller.abort(); }
    await Promise.all([...this.active.values()].map(a => a.promise));
  }
}
