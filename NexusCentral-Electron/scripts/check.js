const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const requiredFiles = [
  'src/index.html',
  'src/styles.css',
  'src/renderer.mjs',
  'src/preload.js',
  'src/main.js',
  'src/workflow-contract.js',
  'src/domain-kits/identity-kit.mjs',
  'src/domain-kits/particle-field-kit.mjs',
  'src/domain-kits/surface-copy-kit.mjs',
  'src/harnesses/coding-harness.mjs',
  'src/services/router-service.js',
  'src/services/models-service.js',
  'src/services/mesh-routing-service.js',
  'sidecars/rust-proof/Cargo.toml',
  'sidecars/rust-proof/src/main.rs'
];

const missing = requiredFiles.filter((file) => !fs.existsSync(path.join(root, file)));
if (missing.length > 0) {
  console.error(`Missing files: ${missing.join(', ')}`);
  process.exit(1);
}

const { workflowSurfaces } = require('../src/workflow-contract');
const expected = [
  { id: 'lab', name: 'Lab' },
  { id: 'setup', name: 'Router' },
  { id: 'models', name: 'Models' },
  { id: 'review', name: 'Review' },
  { id: 'settings', name: 'Settings' }
];
const actual = workflowSurfaces.map((surface) => surface.name);

if (JSON.stringify(actual) !== JSON.stringify(expected.map((surface) => surface.name))) {
  console.error(`Workflow surfaces changed: ${actual.join(' / ')}`);
  process.exit(1);
}

const html = fs.readFileSync(path.join(root, 'src/index.html'), 'utf8');
for (const surface of expected) {
  if (!html.includes(`data-surface="${surface.id}"`)) {
    console.error(`Missing surface in HTML: ${surface.name}`);
    process.exit(1);
  }
}

if (!html.includes('type="module" src="./renderer.mjs"')) {
  console.error('Renderer must load as browser-safe MJS.');
  process.exit(1);
}

for (const internalLabel of ['Conversation Harness', 'Rust sidecar', '/v1/models']) {
  if (html.includes(internalLabel)) {
    console.error(`Internal label leaked into visible HTML: ${internalLabel}`);
    process.exit(1);
  }
}

const renderer = fs.readFileSync(path.join(root, 'src/renderer.mjs'), 'utf8');
if (!renderer.includes('./domain-kits/')) {
  console.error('Renderer must compose through domain kits.');
  process.exit(1);
}

for (const requiredControl of ['create-sandbox', 'refresh-workspace']) {
  if (!html.includes(requiredControl)) {
    console.error(`Missing V1-equivalent visible control: ${requiredControl}`);
    process.exit(1);
  }
}

for (const requiredControl of ['run-loop', 'latest-loop-result', 'review-stage-status', 'act-stage-status', 'audit-stage-status']) {
  if (requiredControl === 'run-loop') {
    if (!html.includes('id="send-message"')) {
      console.error('Missing Lab loop action control.');
      process.exit(1);
    }
    continue;
  }
  if (!html.includes(`id="${requiredControl}"`)) {
    console.error(`Missing Lab loop state: ${requiredControl}`);
    process.exit(1);
  }
}

for (const routerControl of ['router-form', 'router-provider-type', 'router-base-url', 'router-api-key', 'router-model-id', 'router-state', 'router-result']) {
  if (!html.includes(`id="${routerControl}"`)) {
    console.error(`Missing router control: ${routerControl}`);
    process.exit(1);
  }
}

for (const modelControl of ['model-search-form', 'model-search-query', 'model-search-results', 'installed-models']) {
  if (!html.includes(`id="${modelControl}"`)) {
    console.error(`Missing Models control: ${modelControl}`);
    process.exit(1);
  }
}

for (const meshControl of ['mesh-form', 'mesh-node-name', 'mesh-lan-code', 'mesh-state', 'mesh-peers']) {
  if (!html.includes(`id="${meshControl}"`)) {
    console.error(`Missing LAN mesh control: ${meshControl}`);
    process.exit(1);
  }
}

console.log('nexuscentral-electron check passed');
