export const ACTION_VERSION = '1.0.0';
const field = (key, label, type = 'text', extra = {}) => ({ key, label, type, ...extra });
export const ACTIONS = [
  { id: 'records', name: 'Read task records', description: 'Bring your task list into this workflow.', app: 'operations', input: null, output: 'records', tools: ['input.import'], fields: [field('records', 'Task records', 'json', { required: true, help: 'Upload JSON or enter an array of tasks with title, status, owner, and due fields.' })] },
  { id: 'filter', name: 'Select tasks', description: 'Choose the tasks to include.', app: 'operations', input: 'records', output: 'records', tools: [], fields: [field('status', 'Include tasks', 'select', { options: ['all', 'open', 'blocked', 'complete'], default: 'open' })] },
  { id: 'report', name: 'Prepare operations report', description: 'Create a report with owners, progress, and next actions.', app: 'operations', input: 'records', output: 'document', tools: ['artifact.write'], fields: [field('title', 'Report title', 'text', { required: true, default: 'Operations report' }), field('instructions', 'Notes to include', 'textarea')] },
  { id: 'brief', name: 'Prepare a brief', description: 'Capture instructions for a project or recurring routine.', app: 'operations', input: null, output: 'document', tools: ['input.import', 'artifact.write'], fields: [field('title', 'Title', 'text', { required: true, default: 'Project brief' }), field('text', 'Instructions', 'textarea', { required: true })] },
  { id: 'review', name: 'Review result', description: 'Read the result and decide whether it is ready.', app: 'operations', input: 'document', output: 'document', tools: ['review.request'], fields: [field('instructions', 'Review instructions', 'textarea', { default: 'Check the result before marking this workflow complete.' })] }
].map(a => ({ ...a, version: ACTION_VERSION, executor: 'builtin', permissions: a.tools }));
export const APP_CATALOG = [
  { id: 'operations', name: 'Operations', description: 'Task lists, reports, briefs, and review steps.', version: ACTION_VERSION, available: true },
  { id: 'nexusagent', name: 'NexusAgent', description: 'Your specialized agent harnesses.', available: false, reason: 'This app is not available in workflows yet.', repository: 'LuminaryLabs-Dev/NexusAgent' },
  { id: 'codex', name: 'Codex', description: 'Code changes and repository review.', available: false, reason: 'Use existing coding experiments in Settings. Workflow support is not available yet.' },
  { id: 'hermes', name: 'Hermes', description: 'Agent tasks from a supported harness.', available: false, reason: 'This app is not available in workflows yet.' },
  { id: 'claude', name: 'Claude Code', description: 'Agent-assisted development.', available: false, reason: 'This app is not available in workflows yet.' },
  { id: 'antigravity', name: 'Antigravity', description: 'Development surfaces connected to your work.', available: false, reason: 'This app is not available in workflows yet.' },
  { id: 'luminary', name: 'Luminary Framework', description: 'Project references, decisions, and business routines.', available: false, reason: 'Project references are supported. Connected documents and trackers are coming later.' }
];
export const SAMPLE_RECORDS = [
  { id: 'TASK-001', title: 'Review the project deliverables', owner: 'Project lead', status: 'open', due: '' },
  { id: 'TASK-002', title: 'Confirm missing requirements', owner: 'Project lead', status: 'blocked', due: '' },
  { id: 'TASK-003', title: 'Record the kickoff decision', owner: 'Operations', status: 'complete', due: '' }
];
export function operationsTemplate(id) {
  return { id, name: 'Operations review', description: 'Turn task records into a report ready for review.', metadata: { framework: 'luminary', projectRef: '', sourceRefs: [] }, blocks: [
    { id: 'tasks', action: 'records', version: ACTION_VERSION, x: 70, y: 110, config: { records: SAMPLE_RECORDS } },
    { id: 'select', action: 'filter', version: ACTION_VERSION, x: 350, y: 110, config: { status: 'open' } },
    { id: 'report', action: 'report', version: ACTION_VERSION, x: 630, y: 110, config: { title: 'Operations report' } },
    { id: 'review', action: 'review', version: ACTION_VERSION, x: 910, y: 110, config: { instructions: 'Check owners and next actions.' } }
  ], edges: [{ from: 'tasks', to: 'select' }, { from: 'select', to: 'report' }, { from: 'report', to: 'review' }] };
}
export function actionFor(block, actions = ACTIONS) { return actions.find(a => a.id === block.action && a.version === block.version); }
export function validateWorkflow(workflow, { runnable = false, actions = ACTIONS } = {}) {
  const errors = [];
  if (!workflow || !/^[a-zA-Z0-9_-]{1,80}$/.test(workflow.id || '')) return ['Invalid workflow identifier.'];
  if (!workflow.name?.trim() || workflow.name.length > 120) errors.push('Give this workflow a name of up to 120 characters.');
  if (!Array.isArray(workflow.blocks) || !Array.isArray(workflow.edges)) return [...errors, 'Workflow blocks and connections are required.'];
  if (workflow.blocks.length > 40 || workflow.edges.length > 100 || JSON.stringify(workflow).length > 500000) errors.push('This workflow exceeds its size limit.');
  if (runnable && !workflow.blocks.length) errors.push('Add an action before running.');
  const ids = new Set();
  for (const block of workflow.blocks) {
    if (!/^[a-zA-Z0-9_-]{1,80}$/.test(block.id || '') || ids.has(block.id)) errors.push('Each block needs a unique identifier.');
    ids.add(block.id);
    const action = actionFor(block, actions);
    if (!action) { errors.push('An action version is unavailable.'); continue; }
    if (!Number.isFinite(block.x) || !Number.isFinite(block.y) || block.x < 0 || block.y < 0 || block.x > 10000 || block.y > 10000) errors.push('Invalid block position.');
    if (!block.config || typeof block.config !== 'object') { errors.push(`Configure ${action.name}.`); continue; }
    if (runnable) for (const f of action.fields) {
      const value = block.config[f.key];
      if (f.required && (value == null || value === '')) errors.push(`${action.name}: ${f.label} is required.`);
      if (f.type === 'select' && value != null && !f.options.includes(value)) errors.push(`${action.name}: choose a supported ${f.label.toLowerCase()}.`);
    }
    if (runnable && block.action === 'records' && (!Array.isArray(block.config.records) || block.config.records.length > 1000 || block.config.records.some(r => !r || typeof r !== 'object' || typeof r.title !== 'string' || r.title.length > 2000))) errors.push('Task records must contain up to 1,000 objects with a title.');
  }
  const seen = new Set();
  for (const e of workflow.edges) {
    const from = workflow.blocks.find(b => b.id === e.from), to = workflow.blocks.find(b => b.id === e.to);
    const key = `${e.from}:${e.to}`;
    if (!from || !to || from === to || seen.has(key)) { errors.push('A connection is invalid or duplicated.'); continue; }
    seen.add(key);
    if (actionFor(from, actions)?.output !== actionFor(to, actions)?.input) errors.push('These actions use different types of information.');
    if (e.when && !['always', 'has-items', 'empty'].includes(e.when)) errors.push('Unsupported connection condition.');
  }
  const visiting = new Set(), visited = new Set();
  function visit(id) { if (visiting.has(id)) { errors.push('Connections must not form a loop.'); return; } if (visited.has(id)) return; visiting.add(id); workflow.edges.filter(e => e.from === id).forEach(e => visit(e.to)); visiting.delete(id); visited.add(id); }
  workflow.blocks.forEach(b => visit(b.id));
  if (runnable) for (const b of workflow.blocks) if (actionFor(b, actions)?.input && !workflow.edges.some(e => e.to === b.id)) errors.push(`${actionFor(b, actions).name} needs an input connection.`);
  return [...new Set(errors)];
}
