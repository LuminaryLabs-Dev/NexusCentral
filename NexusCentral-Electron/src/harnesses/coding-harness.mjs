export const codingHarness = {
  id: 'coding-agent',
  name: 'Coding agent',
  capabilities: ['workspace.read', 'workspace.write', 'command.run', 'result.audit'],
  approval: {
    writes: 'explicit-lab-action',
    commands: 'workspace-scoped',
    external: 'blocked'
  }
};

export const labStages = [
  { id: 'review', label: 'Review', purpose: 'Understand the request and inspect the workspace.' },
  { id: 'act', label: 'Act', purpose: 'Make the smallest required code changes and run focused checks.' },
  { id: 'audit', label: 'Audit', purpose: 'Inspect the diff and verify the result.' }
];

export function buildCodingPrompt(stage, task, workspacePath, reviewOutput = '') {
  const boundary = [
    `Workspace: ${workspacePath}`,
    'Work only inside the selected workspace.',
    'Do not commit, push, deploy, contact external services, or change files outside the workspace.',
    'Keep the change focused on the requested task.',
    'Return a concise final report with changed files, checks run, and any blocker.'
  ].join('\n');

  if (stage === 'review') {
    return [
      'Review this coding request before any edits.',
      boundary,
      `Request: ${task}`,
      'Inspect the relevant files and current repository state. Do not modify anything.',
      'Report the likely implementation surface, risks, and the smallest viable action.'
    ].join('\n\n');
  }

  if (stage === 'act') {
    return [
      'Act on this coding request.',
      boundary,
      `Request: ${task}`,
      `Review from the previous stage:\n${reviewOutput}`,
      'Implement the requested change. Run the smallest relevant validation commands. Do not stop at a plan.'
    ].join('\n\n');
  }

  return [
    'Audit the coding work that just completed.',
    boundary,
    `Original request: ${task}`,
    `Review from the first stage:\n${reviewOutput}`,
    'Inspect the current diff and validation results. Do not edit files. Report whether the request is complete, what passed, and what remains.'
  ].join('\n\n');
}
