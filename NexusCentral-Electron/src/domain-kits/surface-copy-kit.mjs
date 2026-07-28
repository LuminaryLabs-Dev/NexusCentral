export const surfaceCopyKit = {
  id: 'nexuscentral-outcome-surface-copy-kit',
  domain: 'workflow-copy',
  surfaces: {
    lab: {
      title: 'Lab',
      summary: 'Describe the change, and the Lab will review the workspace, act on it, and audit the result.'
    },
    setup: {
      title: 'Configure providers.',
      summary: 'Choose a provider, verify its endpoint, and select the model used by services.'
    },
    models: {
      title: 'Manage local models.',
      summary: 'Find, download, load, and publish models through the local model service.'
    },
    review: {
      title: 'Review the proof.',
      summary: 'Check recent runs, generated output, and notes without crowding the Lab.'
    },
    settings: {
      title: 'Tune the local app.',
      summary: 'Keep paths and diagnostics away from the main workflow.'
    }
  }
};

export function getSurfaceCopy(surfaceId) {
  return surfaceCopyKit.surfaces[surfaceId] || surfaceCopyKit.surfaces.lab;
}

export function summarizeStatus() {
  return 'Local workbench ready';
}
