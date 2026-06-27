import * as THREE from 'three';

/**
 * Level Manager
 * Handles the infinite floor, obstacle spawning, and collision detection.
 */
export default class LevelManager {
    constructor(scene) {
        this.scene = scene;
        this.obstacles = [];
        this.particles = [];
        
        // Config
        this.spawnZ = -50;
        this.despawnZ = 10;
        this.laneWidth = 3;
        
        // Visuals
        this.gridHelper = null;
        this.floorPlane = null;
        
        this.initEnvironment();
    }

    initEnvironment() {
        // Moving Grid Floor
        this.gridHelper = new THREE.GridHelper(200, 100, 0xff00ff, 0x220033);
        this.gridHelper.position.y = -0.5;
        this.gridHelper.position.z = -50;
        this.scene.add(this.gridHelper);

        // A secondary floor plane to hide grid lines below
        const planeGeo = new THREE.PlaneGeometry(200, 200);
        const planeMat = new THREE.MeshBasicMaterial({ color: 0x050510 });
        this.floorPlane = new THREE.Mesh(planeGeo, planeMat);
        this.floorPlane.rotation.x = -Math.PI / 2;
        this.floorPlane.position.y = -0.6;
        this.floorPlane.position.z = -50;
        this.scene.add(this.floorPlane);
    }

    reset() {
        // Clear existing obstacles
        this.obstacles.forEach(obs => this.scene.remove(obs.mesh));
        this.obstacles = [];
        
        // Reset grid position
        this.gridHelper.position.z = -50;
        this.floorPlane.position.z = -50;
        
        // Reset spawn timer
        this.spawnTimer = 0;
    }

    update(dt, speed, playerPos) {
        // 1. Move Environment (Illusion of speed)
        const moveDist = speed * dt * 20;
        this.gridHelper.position.z += moveDist;
        this.floorPlane.position.z += moveDist;

        // Loop the grid texture/position
        if (this.gridHelper.position.z > 0) {
            this.gridHelper.position.z = -50;
            this.floorPlane.position.z = -50;
        }

        // 2. Spawn Obstacles
        this.spawnTimer -= dt;
        if (this.spawnTimer <= 0) {
            this.spawnObstacle();
            // Randomize next spawn time based on speed (faster speed = faster spawns)
            this.spawnTimer = (1.5 / speed) * (0.5 + Math.random() * 0.5);
        }

        // 3. Update Obstacles & Check Collision
        for (let i = this.obstacles.length - 1; i >= 0; i--) {
            const obs = this.obstacles[i];
            
            // Move obstacle towards camera (relative to player movement)
            // Actually, in this runner style, the player stays mostly static in Z, 
            // and the world moves. But for collision logic, it's easier to think 
            // of objects moving Z+.
            obs.mesh.position.z += moveDist;

            // Rotation for visual flair
            obs.mesh.rotation.x += dt;
            obs.mesh.rotation.y += dt;

            // Collision Check (AABB approximation)
            // Player is at playerPos. Obstacle is at obs.mesh.position.
            // We only care if Z is close and X is close.
            
            const zDist = obs.mesh.position.z - playerPos.z;
            const xDist = Math.abs(obs.mesh.position.x - playerPos.x);

            // Hitbox size
            const hitZ = 0.8; 
            const hitX = 0.8;

            if (zDist > -hitZ && zDist < hitZ && xDist < hitX) {
                return true; // Collision detected
            }

            // Despawn
            if (obs.mesh.position.z > this.despawnZ) {
                this.scene.remove(obs.mesh);
                this.obstacles.splice(i, 1);
            }
        }

        return false; // No collision
    }

    spawnObstacle() {
        // Pick a random lane (-1, 0, 1)
        const lane = Math.floor(Math.random() * 3) - 1;
        const xPos = lane * this.laneWidth;

        // Create Geometry
        const size = 1.2;
        const geometry = new THREE.BoxGeometry(size, size, size);
        
        // Neon Red Material
        const material = new THREE.MeshStandardMaterial({
            color: 0xff0055,
            emissive: 0x550022,
            emissiveIntensity: 0.8,
            roughness: 0.1
        });

        const mesh = new THREE.Mesh(geometry, material);
        mesh.position.set(xPos, 0, this.spawnZ);
        mesh.castShadow = true;
        mesh.receiveShadow = true;

        this.scene.add(mesh);
        this.obstacles.push({ mesh });
    }
}
