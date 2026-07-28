const workflowSurfaces = [
  {
    id: 'lab',
    name: 'Lab',
    purpose: 'Ask, attach files, and work inside the selected workspace.',
    heroAction: 'Send'
  },
  {
    id: 'setup',
    name: 'Router',
    purpose: 'Configure provider endpoints, credentials, and the active model.',
    heroAction: 'Test provider'
  },
  {
    id: 'models',
    name: 'Models',
    purpose: 'Find, download, load, and publish local models.',
    heroAction: 'Find models'
  },
  {
    id: 'review',
    name: 'Review',
    purpose: 'Inspect proof runs, generated projects, scoreboards, and learning notes.',
    heroAction: 'Review output'
  },
  {
    id: 'settings',
    name: 'Settings',
    purpose: 'Manage local paths, appearance, provider references, and advanced options.',
    heroAction: 'Open settings'
  }
];

const desktopCapabilities = [
  'router-endpoint-registry',
  'local-model-lifecycle',
  'workspace-file-browse',
  'validation-proof-review',
  'rust-sidecar'
];

module.exports = {
  workflowSurfaces,
  desktopCapabilities
};
