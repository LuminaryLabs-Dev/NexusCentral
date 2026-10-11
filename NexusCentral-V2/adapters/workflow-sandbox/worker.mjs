// Fixed, trusted transformations only. Configuration and input are data, never code.
import { readFileSync, writeFileSync } from 'node:fs';
const { action, config, inputs } = JSON.parse(readFileSync('input.json', 'utf8'));
let output;
const esc = value => String(value ?? '').replace(/[\r\n|]/g, ' ');
if (action === 'records') output = config.records;
else if (action === 'filter') output = inputs.flat().filter(r => config.status === 'all' || (config.status === 'open' ? !['complete', 'completed', 'done'].includes(String(r.status).toLowerCase()) : r.status === config.status));
else if (action === 'brief') output = `# ${config.title}\n\n${config.text}\n`;
else if (action === 'report') {
  const records = inputs.flat();
  output = `# ${config.title}\n\n${records.length} tasks included.\n\n| Task | Owner | Status | Due |\n| --- | --- | --- | --- |\n` + records.map(r => `| ${esc(r.title)} | ${esc(r.owner || 'Unassigned')} | ${esc(r.status || 'open')} | ${esc(r.due || 'Not set')} |`).join('\n') + '\n\n## Next actions\n\n' + records.filter(r => !['complete','completed','done'].includes(r.status)).map(r => `- ${esc(r.title)} — ${esc(r.owner || 'Assign an owner')}${r.status === 'blocked' ? ' (resolve blocker)' : ''}`).join('\n') + (config.instructions ? `\n\n## Notes\n\n${config.instructions}` : '') + '\n';
} else throw new Error('Unsupported built-in action.');
writeFileSync('result.json', JSON.stringify({ output }));
