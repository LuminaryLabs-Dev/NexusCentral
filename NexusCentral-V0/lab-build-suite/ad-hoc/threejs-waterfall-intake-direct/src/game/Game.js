import * as THREE from 'three';
import InputManager from './input.js';
import LevelManager from './level.js';
import HUDManager from '../ui/hud.js';

/**
 * Core Game Engine
 * Manages the Three.js scene, game loop, and state transitions.
 */
export default class Game {
    constructor() {
        this.container = document.getElementById('game-container');
        this.isPlaying = false;
        this.isPaused = false;
        this.score = 0;
        this.distance = 0;
        this.speed = 0.5; // Base speed
        this.baseSpeed = 0.5;
        this.lastTime = 0;

        // Systems
        this.input = new InputManager();
        this.hud = new HUDManager();
        this.level = null;

        // Three.js Core
        this.scene = null;
        this.camera = null;
        this.renderer = null;
        this.player = null;
        
        // Player Physics/Movement
        this.playerX = 0;
        this.targetPlayerX = 0;
        this.laneWidth = 3; // Distance between lanes
        this.currentLane = 0; // -1 (left), 0 (center), 1 (right)

        this.initThreeJS();
    }

    initThreeJS() {
        // Scene
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x050510);
        this.scene.fog = new THREE.FogExp2(0x050510, 0.035);

        // Camera
        this.camera = new THREE.PerspectiveCamera(60, window.innerWidth / window.innerHeight, 0.1, 100);
        this.camera.position.set(0, 3, 6);
        this.camera.lookAt(0, 0, -5);

        // Renderer
        this.renderer = new THREE.WebGLRenderer({ antialias: true, alpha: false });
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.shadowMap.enabled = true;
        this.container.appendChild(this.renderer.domElement);

        // Lighting
        const ambientLight = new THREE.AmbientLight(0xffffff, 0.4);
        this.scene.add(ambientLight);

        const dirLight = new THREE.DirectionalLight(0xffffff, 0.8);
        dirLight.position.set(5, 10, 5);
        dirLight.castShadow = true;
        this.scene.add(dirLight);

        // Player Setup
        this.createPlayer();

        // Level Setup
        this.level = new LevelManager(this.scene);

        // Bind UI Events
        this.hud.onStart(() => this.startGame());
        this.hud.onRestart(() => this.restartGame());
    }

    createPlayer() {
        // Ship Geometry
        const geometry = new THREE.ConeGeometry(0.5, 1.5, 4);
        geometry.rotateX(Math.PI / 2); // Point forward
        geometry.rotateY(Math.PI / 4); // Flat bottom

        // Neon Material
        const material = new THREE.MeshStandardMaterial({
            color: 0x00f3ff,
            emissive: 0x0044aa,
            emissiveIntensity: 0.5,
            roughness: 0.2,
            metalness: 0.8
        });

        this.player = new THREE.Mesh(geometry, material);
        this.player.castShadow = true;
        this.player.position.y = 0.5;
        this.scene.add(this.player);

        // Engine glow (PointLight)
        const engineLight = new THREE.PointLight(0x00f3ff, 2, 10);
        engineLight.position.set(0, 0.5, 0.8);
        this.player.add(engineLight);
    }

    boot() {
        // Initial render
        this.renderer.render(this.scene, this.camera);
        this.hud.showStartScreen();
    }

    startGame() {
        this.isPlaying = true;
        this.score = 0;
        this.distance = 0;
        this.speed = this.baseSpeed;
        this.currentLane = 0;
        this.playerX = 0;
        this.targetPlayerX = 0;
        this.player.position.set(0, 0.5, 0);
        this.player.rotation.z = 0;
        
        this.level.reset();
        this.hud.startGame();
        
        this.lastTime = performance.now();
        requestAnimationFrame((t) => this.gameLoop(t));
    }

    restartGame() {
        this.startGame();
    }

    gameOver() {
        this.isPlaying = false;
        this.hud.showGameOver(this.score, Math.floor(this.distance));
        
        // Simple explosion effect (visual only)
        this.player.visible = false;
        // Create a temporary particle burst (simplified)
        const burstGeo = new THREE.SphereGeometry(0.1, 8, 8);
        const burstMat = new THREE.MeshBasicMaterial({ color: 0xffaa00 });
        for(let i=0; i<10; i++) {
            const p = new THREE.Mesh(burstGeo, burstMat);
            p.position.copy(this.player.position);
            p.position.x += (Math.random() - 0.5);
            p.position.y += (Math.random() - 0.5);
            this.scene.add(p);
            // Animate out quickly
            const dir = new THREE.Vector3(Math.random()-0.5, Math.random()-0.5, Math.random()-0.5).normalize();
            const speed = 0.2;
            const animateBurst = () => {
                p.position.addScaledVector(dir, speed);
                p.scale.multiplyScalar(0.9);
                if(p.scale.x < 0.01) {
                    this.scene.remove(p);
                } else {
                    requestAnimationFrame(animateBurst);
                }
            };
            animateBurst();
        }
    }

    onWindowResize() {
        if (!this.camera || !this.renderer) return;
        this.camera.aspect = window.innerWidth / window.innerHeight;
        this.camera.updateProjectionMatrix();
        this.renderer.setSize(window.innerWidth, window.innerHeight);
    }

    gameLoop(time) {
        if (!this.isPlaying) return;

        const deltaTime = (time - this.lastTime) / 1000;
        this.lastTime = time;

        this.update(deltaTime);
        this.render();

        requestAnimationFrame((t) => this.gameLoop(t));
    }

    update(dt) {
        // 1. Difficulty Scaling
        this.speed += dt * 0.01; // Slowly increase speed
        this.distance += this.speed * dt * 10;
        this.score += Math.floor(this.speed * 10);

        // 2. Input Handling (Lane Switching)
        if (this.input.isLeftPressed && this.currentLane > -1) {
            this.currentLane--;
            this.input.isLeftPressed = false; // Consume input
        }
        if (this.input.isRightPressed && this.currentLane < 1) {
            this.currentLane++;
            this.input.isRightPressed = false; // Consume input
        }

        // 3. Player Movement (Lerp for smoothness)
        this.targetPlayerX = this.currentLane * this.laneWidth;
        this.playerX += (this.targetPlayerX - this.playerX) * 10 * dt;
        this.player.position.x = this.playerX;

        // Banking effect (tilt ship when turning)
        const tilt = (this.playerX - this.targetPlayerX) * 0.5;
        this.player.rotation.z = tilt;

        // 4. Level & Collision
        const collision = this.level.update(dt, this.speed, this.player.position);
        
        if (collision) {
            this.gameOver();
        }

        // 5. HUD Update
        this.hud.updateScore(this.score);
        this.hud.updateSpeed(Math.floor((this.speed / this.baseSpeed) * 100));
    }

    render() {
        this.renderer.render(this.scene, this.camera);
    }
}
