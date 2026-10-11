import { createStateKit } from '../_shared/create-state-kit.mjs';
import { ACTIONS, APP_CATALOG, validateWorkflow } from '../../packages/workflows/catalog.mjs';
export function createCentralWorkflowsKit() {
  return createStateKit({ id: 'n-central-workflows-kit', domain: 'workflows', domainPath: 'n:central:workflows', apiName: 'centralWorkflows', name: 'Workflows', summary: 'Versioned workflow definitions, runs, approvals, and installed action packs.', requires: ['n:central:jobs', 'n:central:artifacts', 'n:central:audit'], outputs: ['workflows.changed'], resetPolicy: 'preserve definitions and historical runs', initialState: { definitions: [], history: [], runs: [], apps: [{ id: 'operations', version: '1.0.0', enabled: true }] }, transitions: {
    save(state, { definition, expectedRevision }) {
      const errors = validateWorkflow(definition); if (errors.length) throw new Error(errors.join(' '));
      const current = state.definitions.find(d => d.id === definition.id);
      if ((current?.revision || 0) !== Number(expectedRevision || 0)) throw new Error('This workflow changed elsewhere. Reopen it before saving.');
      const next = { ...definition, revision: (current?.revision || 0) + 1 };
      if (current) state.history.push(current);
      state.definitions = [...state.definitions.filter(d => d.id !== next.id), next];
      return { state, result: next };
    },
    remove(state, { id }) { if (state.runs.some(r => r.definition.id === id && ['running', 'paused', 'needs-review'].includes(r.status))) throw new Error('Stop active runs before removing this workflow.'); state.definitions = state.definitions.filter(d => d.id !== id); return { state }; },
    putRun(state, { run }) { state.runs = [...state.runs.filter(r => r.id !== run.id), run]; return { state, result: run }; },
    configureApp(state, { id, enabled }) { const app = APP_CATALOG.find(a => a.id === id); if (!app?.available) throw new Error('This integration is not ready for workflow execution.'); if (!enabled && state.definitions.some(d => d.blocks.some(b => ACTIONS.find(a => a.id === b.action)?.app === id))) throw new Error('This app is used by a saved workflow. Remove those actions first.'); state.apps = [...state.apps.filter(a => a.id !== id), { id, version: app.version, enabled: Boolean(enabled) }]; return { state }; }
  }, reset(state) { return state; } });
}
