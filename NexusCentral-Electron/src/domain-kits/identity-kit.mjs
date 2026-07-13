export const identityKit = {
  id: 'nexuscentral-v1-green-identity-kit',
  domain: 'visual-identity',
  tokens: {
    '--bg': '#081d19',
    '--bg-warm': '#0b2826',
    '--panel': 'rgba(26, 64, 56, 0.88)',
    '--panel-soft': 'rgba(16, 45, 42, 0.76)',
    '--text': '#e6f2f1',
    '--muted': '#91a3a8',
    '--line': 'rgba(75, 116, 104, 0.34)',
    '--accent': '#5ed0a5',
    '--accent-strong': '#5ed0a5',
    '--gold': '#e0af68'
  }
};

export function installIdentity(root = document.documentElement) {
  root.dataset.identity = 'v1-green';
  for (const [name, value] of Object.entries(identityKit.tokens)) {
    root.style.setProperty(name, value);
  }
}
