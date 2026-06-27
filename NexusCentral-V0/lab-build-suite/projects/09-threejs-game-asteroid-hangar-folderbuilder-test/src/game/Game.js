import * as THREE from 'three';
import { Level } from './level.js';
import { InputHandler } from './input.js';
import { HUD } from '../ui/hud.js';

export class Game {
    constructor(canvas) {
        this.canvas = canvas;
        this.scene = null;
        this.camera = null;
        this.renderer = null;
        this.clock = new THREE.Clock();
        this.isRunning = false;
        this.isPaused = false;
        this.gameState = 'start'; // start, playing, paused, won, lost
        
        // Game entities
        this.ship = null;
        this.level = null;
        this.input = null;
        this.hud = null;
        
        // Game stats
        this.score = 0;
        this.energy = 100;
        this.maxEnergy = 100;
        this.progress = 0;
        this.maxProgress = 100;
        
        // Ship physics
        this.shipVelocity = new THREE.Vector3();
        this.shipSpeed = 30;
        this.shipRotationSpeed = 2.5;
        this.shipAcceleration = 50;
        this.shipFriction = 0.95;
        
        // Camera follow
        this.cameraOffset = new THREE.Vector3(0, 8, 20);
        this.cameraLookOffset = new THREE.Vector3(0, 0, -30);
        this.cameraSmoothness = 0.1;
        
        this.init();
    }
    
    init() {
        // Create scene
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x0a0a15);
        this.scene.fog = new THREE.Fog(0x0a0a15, 50, 200);
        
        // Create camera
        this.camera = new THREE.PerspectiveCamera(
            75,
            window.innerWidth / window.innerHeight,
            0.1,
            1000
        );
        
        // Create renderer
        this.renderer = new THREE.WebGLRenderer({
            canvas: this.canvas,
            antialias: true
        });
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        
        // Add lighting
        this.setupLighting();
        
        // Create ship
        this.createShip();
        
        // Create level
        this.level = new Level(this.scene);
        
        // Setup input
        this.input = new InputHandler();
        
        // Setup HUD
        this.hud = new HUD();
        
        // Show start message
        this.hud.showMessage('ASTEROID HANGAR', 
            'Pilot your ship through the asteroid field!\n\n' +
            'Controls:\n' +
            'W/↑ - Accelerate Forward\n' +
            'S/↓ - Decelerate/Reverse\n' +
            'A/← - Turn Left\n' +
            'D/→ - Turn Right\n' +
            'SPACE - Boost\n' +
            'P/ESC - Pause\n\n' +
            'Collect energy cells to maintain power.\n' +
            'Reach the docking ring to win!',
            'START GAME',
            () => this.startGame()
        );
    }
    
    setupLighting() {
        // Ambient light
        const ambientLight = new THREE.AmbientLight(0x404060, 0.5);
        this.scene.add(ambientLight);
        
        // Main directional light (sun)
        const sunLight = new THREE.DirectionalLight(0xffffff, 1);
        sunLight.position.set(50, 100, 50);
        sunLight.castShadow = true;
        sunLight.shadow.mapSize.width = 2048;
        sunLight.shadow.mapSize.height = 2048;
        sunLight.shadow.camera.near = 0.5;
        sunLight.shadow.camera.far = 500;
        sunLight.shadow.camera.left = -100;
        sunLight.shadow.camera.right = 100;
        sunLight.shadow.camera.top = 100;
        sunLight.shadow.camera.bottom = -100;
        this.scene.add(sunLight);
        
        // Point lights for atmosphere
        const pointLight1 = new THREE.PointLight(0x4488ff, 0.5, 100);
        pointLight1.position.set(-30, 20, -30);
        this.scene.add(pointLight1);
        
        const pointLight2 = new THREE.PointLight(0xff4444, 0.3, 80);
        pointLight2.position.set(30, 15, 30);
        this.scene.add(pointLight2);
    }
    
    createShip() {
        // Ship group
        this.ship = new THREE.Group();
        
        // Main body
        const bodyGeometry = new THREE.ConeGeometry(1, 4, 8);
        const bodyMaterial = new THREE.MeshStandardMaterial({
            color: 0x3366cc,
            metalness: 0.8,
            roughness: 0.3
        });
        const body = new THREE.Mesh(bodyGeometry, bodyMaterial);
        body.rotation.x = Math.PI / 2;
        body.castShadow = true;
        this.ship.add(body);
        
        // Cockpit
        const cockpitGeometry = new THREE.SphereGeometry(0.6, 16, 16);
        const cockpitMaterial = new THREE.MeshStandardMaterial({
            color: 0x88ccff,
            metalness: 0.9,
            roughness: 0.1,
            transparent: true,
            opacity: 0.7
        });
        const cockpit = new THREE.Mesh(cockpitGeometry, cockpitMaterial);
        cockpit.position.set(0, 0.5, -0.5);
        this.ship.add(cockpit);
        
        // Engine glow
        const engineGeometry = new THREE.CylinderGeometry(0.3, 0.5, 1, 8);
        const engineMaterial = new THREE.MeshStandardMaterial({
            color: 0xff6600,
            emissive: 0xff4400,
            emissiveIntensity: 0.5
        });
        const engine = new THREE.Mesh(engineGeometry, engineMaterial);
        engine.rotation.x = Math.PI / 2;
        engine.position.z = 2;
        this.ship.add(engine);
        
        // Wings
        const wingGeometry = new THREE.BoxGeometry(4, 0.1, 1.5);
        const wingMaterial = new THREE.MeshStandardMaterial({
            color: 0x2255aa,
            metalness: 0.7,
            roughness: 0.4
        });
        const wings = new THREE.Mesh(wingGeometry, wingMaterial);
        wings.position.z = 0.5;
        wings.castShadow = true;
        this.ship.add(wings);
        
        // Wing tips
        const tipGeometry = new THREE.BoxGeometry(0.5, 0.3, 0.8);
        const tipMaterial = new THREE.MeshStandardMaterial({
            color: 0xff3333,
            emissive: 0xff0000,
            emissiveIntensity: 0.3
        });
        
        const leftTip = new THREE.Mesh(tipGeometry, tipMaterial);
        leftTip.position.set(-2, 0, 0.5);
        this.ship.add(leftTip);
        
        const rightTip = new THREE.Mesh(tipGeometry, tipMaterial);
        rightTip.position.set(2, 0, 0.5);
        this.ship.add(rightTip);
        
        // Position ship
        this.ship.position.set(0, 5, 0);
        this.scene.add(this.ship);
    }
    
    startGame() {
        this.gameState = 'playing';
        this.isRunning = true;
        this.isPaused = false;
        
        // Reset stats
        this.score = 0;
        this.energy = this.maxEnergy;
        this.progress = 0;
        
        // Reset ship
        this.ship.position.set(0, 5, 0);
        this.ship.rotation.set(0, 0, 0);
        this.shipVelocity.set(0, 0, 0);
        
        // Reset level
        this.level.reset();
        
        // Update HUD
        this.hud.updateScore(this.score);
        this.hud.updateEnergy(this.energy, this.maxEnergy);
        this.hud.updateProgress(this.progress, this.maxProgress);
        this.hud.hideMessage();
    }
    
    start() {
        this.isRunning = true;
        this.animate();
    }
    
    pause() {
        if (this.gameState === 'playing') {
            this.isPaused = true;
            this.gameState = 'paused';
            this.hud.showPause();
        }
    }
    
    resume() {
        if (this.gameState === 'paused') {
            this.isPaused = false;
            this.gameState = 'playing';
            this.hud.hidePause();
        }
    }
    
    restart() {
        this.startGame();
    }
    
    win() {
        this.gameState = 'won';
        this.isRunning = false;
        this.hud.showMessage('MISSION COMPLETE!', 
            `Congratulations! You reached the docking ring!\n\nFinal Score: ${this.score}`,
            'PLAY AGAIN',
            () => this.restart()
        );
    }
    
    lose() {
        this.gameState = 'lost';
        this.isRunning = false;
        this.hud.showMessage('MISSION FAILED', 
            `Your ship was destroyed!\n\nFinal Score: ${this.score}`,
            'TRY AGAIN',
            () => this.restart()
        );
    }
    
    handleResize() {
        this.camera.aspect = window.innerWidth / window.innerHeight;
        this.camera.updateProjectionMatrix();
        this.renderer.setSize(window.innerWidth, window.innerHeight);
    }
    
    update(deltaTime) {
        if (!this.isRunning || this.isPaused) return;
        
        // Handle input
        this.handleInput(deltaTime);
        
        // Update ship physics
        this.updateShip(deltaTime);
        
        // Update camera
        this.updateCamera();
        
        // Check collisions
        this.checkCollisions();
        
        // Update level
        this.level.update(deltaTime, this.ship.position);
        
        // Update progress based on distance traveled
        this.updateProgress();
        
        // Drain energy slowly
        this.energy -= deltaTime * 2;
        if (this.energy <= 0) {
            this.lose();
            return;
        }
        
        // Update HUD
        this.hud.updateEnergy(this.energy, this.maxEnergy);
        this.hud.updateProgress(this.progress, this.maxProgress);
    }
    
    handleInput(deltaTime) {
        const input = this.input;
        
        // Check for pause
        if (input.pausePressed) {
            if (this.isPaused) {
                this.resume();
            } else {
                this.pause();
            }
            input.pausePressed = false;
            return;
        }
        
        if (this.isPaused) return;
        
        // Rotation
        if (input.left) {
            this.ship.rotation.y += this.shipRotationSpeed * deltaTime;
        }
        if (input.right) {
            this.ship.rotation.y -= this.shipRotationSpeed * deltaTime;
        }
        
        // Calculate forward direction
        const forward = new THREE.Vector3(0, 0, -1);
        forward.applyQuaternion(this.ship.quaternion);
        
        // Acceleration
        let acceleration = this.shipAcceleration;
        if (input.boost && this.energy > 0) {
            acceleration *= 2;
            this.energy -= deltaTime * 15;
        }
        
        if (input.forward) {
            this.shipVelocity.add(forward.multiplyScalar(acceleration * deltaTime));
        }
        if (input.backward) {
            this.shipVelocity.add(forward.multiplyScalar(-acceleration * 0.5 * deltaTime));
        }
    }
    
    updateShip(deltaTime) {
        // Apply friction
        this.shipVelocity.multiplyScalar(this.shipFriction);
        
        // Clamp velocity
        const speed = this.shipVelocity.length();
        if (speed > this.shipSpeed) {
            this.shipVelocity.normalize().multiplyScalar(this.shipSpeed);
        }
        
        // Update position
        this.ship.position.add(this.shipVelocity.clone().multiplyScalar(deltaTime));
        
        // Keep ship within bounds
        const bounds = 80;
        this.ship.position.x = Math.max(-bounds, Math.min(bounds, this.ship.position.x));
        this.ship.position.y = Math.max(2, Math.min(30, this.ship.position.y));
        this.ship.position.z = Math.max(-bounds, Math.min(bounds, this.ship.position.z));
        
        // Tilt ship based on velocity
        this.ship.rotation.z = -this.shipVelocity.x * 0.02;
        this.ship.rotation.x = this.shipVelocity.z * 0.01;
    }
    
    updateCamera() {
        // Calculate target camera position
        const targetPosition = this.ship.position.clone().add(
            this.cameraOffset.clone().applyQuaternion(this.ship.quaternion)
        );
        
        // Smooth camera movement
        this.camera.position.lerp(targetPosition, this.cameraSmoothness);
        
        // Look at ship
        const lookTarget = this.ship.position.clone().add(
            this.cameraLookOffset.clone().applyQuaternion(this.ship.quaternion)
        );
        this.camera.lookAt(lookTarget);
    }
    
    checkCollisions() {
        const shipBox = new THREE.Box3().setFromObject(this.ship);
        const shipRadius = 2;
        
        // Check asteroid collisions
        for (const asteroid of this.level.asteroids) {
            const distance = this.ship.position.distanceTo(asteroid.position);
            const asteroidRadius = asteroid.userData.radius || 3;
            
            if (distance < shipRadius + asteroidRadius) {
                // Collision!
                this.handleCollision(asteroid, 'asteroid');
            }
        }
        
        // Check energy cell collisions
        for (let i = this.level.energyCells.length - 1; i >= 0; i--) {
            const cell = this.level.energyCells[i];
            const distance = this.ship.position.distanceTo(cell.position);
            
            if (distance < shipRadius + 1.5) {
                // Collect energy cell
                this.collectEnergyCell(cell, i);
            }
        }
        
        // Check docking ring collision
        const dockingRing = this.level.dockingRing;
        if (dockingRing) {
            const distance = this.ship.position.distanceTo(dockingRing.position);
            if (distance < 8) {
                this.win();
            }
        }
    }
    
    handleCollision(object, type) {
        if (type === 'asteroid') {
            // Bounce back
            const bounceDir = this.ship.position.clone().sub(object.position).normalize();
            this.shipVelocity.add(bounceDir.multiplyScalar(20));
            
            // Damage
            this.energy -= 25;
            
            // Visual feedback
            this.showCollisionEffect(object.position);
            
            // Score penalty
            this.score = Math.max(0, this.score - 50);
            this.hud.updateScore(this.score);
        }
    }
    
    collectEnergyCell(cell, index) {
        // Remove cell from scene and array
        this.scene.remove(cell);
        this.level.energyCells.splice(index, 1);
        
        // Restore energy
        this.energy = Math.min(this.maxEnergy, this.energy + 20);
        
        // Add score
        this.score += 100;
        this.hud.updateScore(this.score);
        
        // Visual feedback
        this.showCollectEffect(cell.position);
    }
    
    showCollisionEffect(position) {
        // Create explosion particles
        const particleCount = 10;
        const particles = [];
        
        for (let i = 0; i < particleCount; i++) {
            const geometry = new THREE.SphereGeometry(0.2, 8, 8);
            const material = new THREE.MeshBasicMaterial({
                color: 0xff4400,
                transparent: true
            });
            const particle = new THREE.Mesh(geometry, material);
            particle.position.copy(position);
            particle.userData.velocity = new THREE.Vector3(
                (Math.random() - 0.5) * 10,
                (Math.random() - 0.5) * 10,
                (Math.random() - 0.5) * 10
            );
            particle.userData.life = 1.0;
            this.scene.add(particle);
            particles.push(particle);
        }
        
        // Animate particles
        const animateParticles = () => {
            let alive = false;
            for (const particle of particles) {
                particle.userData.life -= 0.05;
                if (particle.userData.life > 0) {
                    alive = true;
                    particle.position.add(particle.userData.velocity.clone().multiplyScalar(0.016));
                    particle.material.opacity = particle.userData.life;
                    particle.scale.setScalar(particle.userData.life);
                } else {
                    this.scene.remove(particle);
                }
            }
            if (alive) {
                requestAnimationFrame(animateParticles);
            }
        };
        animateParticles();
    }
    
    showCollectEffect(position) {
        // Create sparkle particles
        const particleCount = 8;
        const particles = [];
        
        for (let i = 0; i < particleCount; i++) {
            const geometry = new THREE.SphereGeometry(0.15, 8, 8);
            const material = new THREE.MeshBasicMaterial({
                color: 0x00ff88,
                transparent: true
            });
            const particle = new THREE.Mesh(geometry, material);
            particle.position.copy(position);
            const angle = (i / particleCount) * Math.PI * 2;
            particle.userData.velocity = new THREE.Vector3(
                Math.cos(angle) * 5,
                Math.sin(angle) * 5,
                0
            );
            particle.userData.life = 0.8;
            this.scene.add(particle);
            particles.push(particle);
        }
        
        const animateParticles = () => {
            let alive = false;
            for (const particle of particles) {
                particle.userData.life -= 0.04;
                if (particle.userData.life > 0) {
                    alive = true;
                    particle.position.add(particle.userData.velocity.clone().multiplyScalar(0.016));
                    particle.material.opacity = particle.userData.life;
                } else {
                    this.scene.remove(particle);
                }
            }
            if (alive) {
                requestAnimationFrame(animateParticles);
            }
        };
        animateParticles();
    }
    
    updateProgress() {
        // Calculate progress based on Z position (moving forward)
        const startZ = 0;
        const endZ = -150;
        const currentZ = this.ship.position.z;
        
        this.progress = Math.min(100, Math.max(0, 
            ((startZ - currentZ) / (startZ - endZ)) * 100
        ));
    }
    
    animate() {
        requestAnimationFrame(() => this.animate());
        
        const deltaTime = this.clock.getDelta();
        this.update(deltaTime);
        
        this.renderer.render(this.scene, this.camera);
    }
}
