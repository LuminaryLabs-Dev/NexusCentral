import * as THREE from 'three';

// --- Configuration ---
const CONFIG = {
  speed: 0.8,
  turnSpeed: 0.05,
  worldLength: 2000,
  asteroidCount: 150,
  energyCount: 30,
  collisionDist: 2.5,
  energyDist: 3.0,
  winDist: 50
};

// --- State ---
let state = {
  status: 'start',
  score: 0,
  energy: 100,
  distance: 0,
  time: 0
};

// --- Scene Setup ---
const scene = new THREE.Scene();
scene.fog = new THREE.FogExp2(0x000000, 0.002);

const camera = new THREE.PerspectiveCamera(75, window.innerWidth / window.innerHeight, 0.1, 1000);
const renderer = new THREE.WebGLRenderer({ antialias: true });
renderer.setSize(window.innerWidth, window.innerHeight);
document.body.appendChild(renderer.domElement);

// --- Lighting ---
const ambientLight = new THREE.AmbientLight(0x404040, 2);
scene.add(ambientLight);

const dirLight = new THREE.DirectionalLight(0xffffff, 2);
dirLight.position.set(10, 20, 10);
scene.add(dirLight);

// --- Objects ---

// Ship
const shipGroup = new THREE.Group();
const shipGeo = new THREE.ConeGeometry(0.5, 2, 8);
shipGeo.rotateX(Math.PI / 2);
const shipMat = new THREE.MeshStandardMaterial({ color: 0x00ffff, roughness: 0.4, metalness: 0.8 });
const shipMesh = new THREE.Mesh(shipGeo, shipMat);
shipGroup.add(shipMesh);

const engineGeo = new THREE.SphereGeometry(0.3, 8, 8);
const engineMat = new THREE.MeshBasicMaterial({ color: 0xffaa00 });
const engineMesh = new THREE.Mesh(engineGeo, engineMat);
engineMesh.position.z = 1;
shipGroup.add(engineMesh);

const shipLight = new THREE.PointLight(0x00ffff, 1, 20);
shipLight.position.set(0, 1, 0);
shipGroup.add(shipLight);

scene.add(shipGroup);

// Asteroids
const asteroids = [];
const asteroidGeo = new THREE.DodecahedronGeometry(1, 0);
const asteroidMat = new THREE.MeshStandardMaterial({ color: 0x888888, roughness: 0.8 });

for (let i = 0; i < CONFIG.asteroidCount; i++) {
  const mesh = new THREE.Mesh(asteroidGeo, asteroidMat);
  const z = -Math.random() * CONFIG.worldLength;
  const r = 10 + Math.random() * 30;
  const theta = Math.random() * Math.PI * 2;
  mesh.position.set(r * Math.cos(theta), r * Math.sin(theta), z);
  
  const scale = 1 + Math.random() * 3;
  mesh.scale.set(scale, scale, scale);
  mesh.rotation.set(Math.random(), Math.random(), Math.random());
  
  scene.add(mesh);
  asteroids.push({ mesh, radius: scale });
}

// Energy Cells
const energies = [];
const energyGeo = new THREE.OctahedronGeometry(0.5);
const energyMat = new THREE.MeshBasicMaterial({ color: 0x00ff00, wireframe: true });

for (let i = 0; i < CONFIG.energyCount; i++) {
  const mesh = new THREE.Mesh(energyGeo, energyMat);
  const z = -Math.random() * CONFIG.worldLength;
  const r = 5 + Math.random() * 20;
  const theta = Math.random() * Math.PI * 2;
  mesh.position.set(r * Math.cos(theta), r * Math.sin(theta), z);
  
  scene.add(mesh);
  energies.push(mesh);
}

// Docking Ring
const ringGeo = new THREE.TorusGeometry(15, 1, 16, 100);
const ringMat = new THREE.MeshBasicMaterial({ color: 0xff00ff });
const ring = new THREE.Mesh(ringGeo, ringMat);
ring.position.set(0, 0, -CONFIG.worldLength - 50);
ring.rotation.y = Math.PI / 2;
scene.add(ring);

const ringLight1 = new THREE.PointLight(0xff00ff, 2, 50);
ringLight1.position.copy(ring.position);
scene.add(ringLight1);

// --- Input ---
const keys = { w: false, a: false, s: false, d: false, ArrowUp: false, ArrowLeft: false, ArrowDown: false, ArrowRight: false };

window.addEventListener('keydown', (e) => {
  if(keys.hasOwnProperty(e.key)) keys[e.key] = true;
  if(e.key === 'Escape' && state.status === 'playing') togglePause();
  else if(e.key === 'Escape' && state.status === 'paused') togglePause();
});

window.addEventListener('keyup', (e) => {
  if(keys.hasOwnProperty(e.key)) keys[e.key] = false;
});

// --- UI Logic ---
const uiScore = document.getElementById('score-display');
const uiEnergy = document.getElementById('energy-display');
const uiProgress = document.getElementById('progress-display');
const msgOverlay = document.getElementById('message-overlay');
const msgTitle = document.getElementById('msg-title');
const msgText = document.getElementById('msg-text');
const actionBtn = document.getElementById('action-btn');

actionBtn.addEventListener('click', () => {
  if (state.status === 'start' || state.status === 'gameover' || state.status === 'win') {
    startGame();
  } else if (state.status === 'paused') {
    togglePause();
  }
});

function updateUI() {
  uiScore.innerText = `Score: ${state.score}`;
  uiEnergy.innerText = `Energy: ${Math.floor(state.energy)}%`;
  uiProgress.innerText = `Distance: ${Math.floor(Math.abs(shipGroup.position.z))}m`;
  
  if (state.status === 'start') {
    msgOverlay.style.display = 'block';
    msgTitle.innerText = 'Asteroid Hangar';
    msgText.innerText = 'Pilot your ship to the docking ring. Avoid asteroids. Collect energy.';
    actionBtn.innerText = 'Start Mission';
  } else if (state.status === 'gameover') {
    msgOverlay.style.display = 'block';
    msgTitle.innerText = 'Mission Failed';
    msgText.innerText = `Ship destroyed. Final Score: ${state.score}`;
    actionBtn.innerText = 'Retry';
  } else if (state.status === 'win') {
    msgOverlay.style.display = 'block';
    msgTitle.innerText = 'Mission Success';
    msgText.innerText = `Docked successfully! Final Score: ${state.score}`;
    actionBtn.innerText = 'Play Again';
  } else if (state.status === 'paused') {
    msgOverlay.style.display = 'block';
    msgTitle.innerText = 'Paused';
    msgText.innerText = 'Press ESC or button to resume';
    actionBtn.innerText = 'Resume';
  } else {
    msgOverlay.style.display = 'none';
  }
}

function startGame() {
  state = { status: 'playing', score: 0, energy: 100, distance: 0, time: 0 };
  shipGroup.position.set(0, 0, 0);
  shipGroup.rotation.set(0, 0, 0);
  energies.forEach(e => {
    e.visible = true;
    const z = -Math.random() * CONFIG.worldLength;
    const r = 5 + Math.random() * 20;
    const theta = Math.random() * Math.PI * 2;
    e.position.set(r * Math.cos(theta), r * Math.sin(theta), z);
  });
  updateUI();
}

function togglePause() {
  if (state.status === 'playing') state.status = 'paused';
  else if (state.status === 'paused') state.status = 'playing';
  updateUI();
}

function gameOver() {
  state.status = 'gameover';
  updateUI();
}

function gameWin() {
  state.status = 'win';
  updateUI();
}

// --- Game Loop ---
function animate() {
  requestAnimationFrame(animate);

  if (state.status === 'playing') {
    if (keys.w || keys.ArrowUp) shipGroup.position.y += CONFIG.speed;
    if (keys.s || keys.ArrowDown) shipGroup.position.y -= CONFIG.speed;
    if (keys.a || keys.ArrowLeft) shipGroup.position.x -= CONFIG.speed;
    if (keys.d || keys.ArrowRight) shipGroup.position.x += CONFIG.speed;

    shipGroup.position.z -= CONFIG.speed * 0.5;

    shipGroup.rotation.z = -(keys.a || keys.ArrowLeft ? 0.3 : 0) + (keys.d || keys.ArrowRight ? 0.3 : 0);
    shipGroup.rotation.x = (keys.w || keys.ArrowUp ? 0.2 : 0) - (keys.s || keys.ArrowDown ? 0.2 : 0);

    camera.position.x = shipGroup.position.x * 0.5;
    camera.position.y = shipGroup.position.y * 0.5 + 5;
    camera.position.z = shipGroup.position.z + 20;
    camera.lookAt(shipGroup.position.x, shipGroup.position.y, shipGroup.position.z - 20);

    for (let a of asteroids) {
      if (Math.abs(a.mesh.position.z - shipGroup.position.z) < 10) {
        const dist = shipGroup.position.distanceTo(a.mesh.position);
        if (dist < CONFIG.collisionDist + a.radius) {
          gameOver();
          shipMat.color.setHex(0xff0000);
          setTimeout(() => shipMat.color.setHex(0x00ffff), 200);
          break;
        }
      }
    }

    energies.forEach(e => {
      if (e.visible) {
        const dist = shipGroup.position.distanceTo(e.position);
        if (dist < CONFIG.energyDist) {
          e.visible = false;
          state.score += 100;
          state.energy = Math.min(100, state.energy + 20);
        }
        e.rotation.y += 0.05;
        e.rotation.x += 0.05;
      }
    });

    const distToRing = shipGroup.position.distanceTo(ring.position);
    if (distToRing < CONFIG.winDist) {
      gameWin();
    }

    state.energy -= 0.05;
    if (state.energy <= 0) {
      gameOver();
    }

    updateUI();
  } else if (state.status === 'paused') {
  } else {
      camera.position.x = Math.sin(Date.now() * 0.0005) * 10;
      camera.position.z = Math.cos(Date.now() * 0.0005) * 10 + 20;
      camera.lookAt(0, 0, -50);
  }

  renderer.render(scene, camera);
}

window.addEventListener('resize', () => {
  camera.aspect = window.innerWidth / window.innerHeight;
  camera.updateProjectionMatrix();
  renderer.setSize(window.innerWidth, window.innerHeight);
});

updateUI();
animate();
