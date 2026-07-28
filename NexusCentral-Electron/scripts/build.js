const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '..');
const outDir = path.join(root, 'dist');

function copyDir(src, dest) {
  fs.mkdirSync(dest, { recursive: true });
  for (const entry of fs.readdirSync(src, { withFileTypes: true })) {
    const from = path.join(src, entry.name);
    const to = path.join(dest, entry.name);
    if (entry.isDirectory()) {
      copyDir(from, to);
    } else if (entry.isFile()) {
      fs.copyFileSync(from, to);
    }
  }
}

fs.rmSync(outDir, { recursive: true, force: true });
fs.mkdirSync(outDir, { recursive: true });
copyDir(path.join(root, 'src'), path.join(outDir, 'src'));
copyDir(path.join(root, 'sidecars'), path.join(outDir, 'sidecars'));
fs.copyFileSync(path.join(root, 'package.json'), path.join(outDir, 'package.json'));
fs.copyFileSync(path.join(root, 'README.md'), path.join(outDir, 'README.md'));

console.log(`Prepared Electron shell at ${outDir}`);
