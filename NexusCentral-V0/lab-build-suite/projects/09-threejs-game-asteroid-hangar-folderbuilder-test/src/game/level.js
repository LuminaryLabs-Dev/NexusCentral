import * as THREE from 'three';

export class Level {
    constructor(scene) {
        this.scene = scene;
        this.asteroids = [];
        this.energyCells = [];
        this.dockingRing = null;
        
        this.createLevel();
    }
    
    createLevel() {
        // Create asteroid field
        this.createAsteroidField();
        
        // Create energy cells
        this.createEnergyCells();
        
        // Create docking ring
        this.createDockingRing();
        
        // Create starfield background
        this.createStarfield();
        
        // Create hangar structure
        this.createHangarStructure();
    }
    
    createAsteroidField() {
        const asteroidCount = 50;
        
        for (let i = 0; i < asteroidCount; i++) {
            const asteroid = this.createAsteroid();
            
            // Position asteroids in a corridor
            const z = -10 - (i * 3) - Math.random() * 5;
            const x = (Math.random() - 0.5) * 60;
            const y = 5 + Math.random() * 15;
            
            asteroid.position.set(x, y, z);
            asteroid.rotation.set(
                Math.random() * Math.PI,
                Math.random() * Math.PI,
                Math.random() * Math.PI
            );
            
            // Store radius for collision
            asteroid.userData.radius = asteroid.userData.size;
            asteroid.userData.rotationSpeed = new THREE.Vector3(
                (Math.random() - 0.5) * 0.5,
                (Math.random() - 0.5) * 0.5,
                (Math.random() - 0.5) * 0.5
            );
            
            this.asteroids.push(asteroid);
            this.scene.add(asteroid);
        }
    }
    
    createAsteroid() {
        const size = 2 + Math.random() * 4;
        const geometry = new THREE.DodecahedronGeometry(size, 1);
        
        // Add some randomness to vertices for more organic look
        const positions = geometry.attributes.position;
        for (let i = 0; i < positions.count; i++) {
            const x = positions.getX(i);
            const y = positions.getY(i);
            const z = positions.getZ(i);
            const noise = (Math.random() - 0.5) * size * 0.3;
            positions.setXYZ(i, x + noise, y + noise, z + noise);
        }
        geometry.computeVertexNormals();
        
        const material = new THREE.MeshStandardMaterial({
            color: new THREE.Color().setHSL(0.08, 0.3, 0.3 + Math.random() * 0.2),
            roughness: 0.8,
            metalness: 0.2,
            flatShading: true
        });
        
        const asteroid = new THREE.Mesh(geometry, material);
        asteroid.castShadow = true;
        asteroid.receiveShadow = true;
        asteroid.userData.size = size;
        
        return asteroid;
    }
    
    createEnergyCells() {
        const cellCount = 15;
        
        for (let i = 0; i < cellCount; i++) {
            const cell = this.createEnergyCell();
            
            // Position cells along the path
            const z = -15 - (i * 10);
            const x = (Math.random() - 0.5) * 40;
            const y = 8 + Math.random() * 10;
            
            cell.position.set(x, y, z);
            
            this.energyCells.push(cell);
            this.scene.add(cell);
        }
    }
    
    createEnergyCell() {
        const group = new THREE.Group();
        
        // Core
        const coreGeometry = new THREE.OctahedronGeometry(0.8, 0);
        const coreMaterial = new THREE.MeshStandardMaterial({
            color: 0x00ff88,
            emissive: 0x00ff88,
            emissiveIntensity: 0.5,
            metalness: 0.9,
            roughness: 0.1
        });
        const core = new THREE.Mesh(coreGeometry, coreMaterial);
        group.add(core);
        
        // Outer shell
        const shellGeometry = new THREE.OctahedronGeometry(1.2, 0);
        const shellMaterial = new THREE.MeshStandardMaterial({
            color: 0x00aa55,
            transparent: true,
            opacity: 0.5,
            metalness: 0.5,
            roughness: 0.3
        });
        const shell = new THREE.Mesh(shellGeometry, shellMaterial);
        group.add(shell);
        
        // Point light
        const light = new THREE.PointLight(0x00ff88, 1, 10);
        group.add(light);
        
        group.userData.core = core;
        group.userData.shell = shell;
        group.userData.rotationSpeed = 2;
        group.userData.pulsePhase = Math.random() * Math.PI * 2;
        
        return group;
    }
    
    createDockingRing() {
        const group = new THREE.Group();
        
        // Main ring
        const ringGeometry = new THREE.TorusGeometry(10, 1, 16, 32);
        const ringMaterial = new THREE.MeshStandardMaterial({
            color: 0x4488ff,
            emissive: 0x2244aa,
            emissiveIntensity: 0.3,
            metalness: 0.8,
            roughness: 0.2
        });
        const ring = new THREE.Mesh(ringGeometry, ringMaterial);
        ring.rotation.x = Math.PI / 2;
        group.add(ring);
        
        // Inner ring
        const innerRingGeometry = new THREE.TorusGeometry(8, 0.5, 16, 32);
        const innerRingMaterial = new THREE.MeshStandardMaterial({
            color: 0x88ccff,
            emissive: 0x4488ff,
            emissiveIntensity: 0.5,
            metalness: 0.9,
            roughness: 0.1
        });
        const innerRing = new THREE.Mesh(innerRingGeometry, innerRingMaterial);
        innerRing.rotation.x = Math.PI / 2;
        group.add(innerRing);
        
        // Guide lights
        for (let i = 0; i < 8; i++) {
            const angle = (i / 8) * Math.PI * 2;
            const lightGeometry = new THREE.SphereGeometry(0.3, 8, 8);
            const lightMaterial = new THREE.MeshStandardMaterial({
                color: 0x00ff00,
                emissive: 0x00ff00,
                emissiveIntensity: 1
            });
            const lightMesh = new THREE.Mesh(lightGeometry, lightMaterial);
            lightMesh.position.set(
                Math.cos(angle) * 9,
                0,
                Math.sin(angle) * 9
            );
            group.add(lightMesh);
        }
        
        // Position at end of level
        group.position.set(0, 10, -150);
        
        this.dockingRing = group;
        this.scene.add(group);
    }
    
    createStarfield() {
        const starCount = 1000;
        const geometry = new THREE.BufferGeometry();
        const positions = new Float32Array(starCount * 3);
        
        for (let i = 0; i < starCount; i++) {
            positions[i * 3] = (Math.random() - 0.5) * 400;
            positions[i * 3 + 1] = (Math.random() - 0.5) * 400;
            positions[i * 3 + 2] = (Math.random() - 0.5) * 400;
        }
        
        geometry.setAttribute('position', new THREE.BufferAttribute(positions, 3));
        
        const material = new THREE.PointsMaterial({
            color: 0xffffff,
            size: 0.5,
            transparent: true,
            opacity: 0.8
        });
        
        const stars = new THREE.Points(geometry, material);
        this.scene.add(stars);
    }
    
    createHangarStructure() {
        // Floor grid
        const gridHelper = new THREE.GridHelper(200, 50, 0x224466, 0x112233);
        gridHelper.position.y = 0;
        this.scene.add(gridHelper);
        
        // Side walls (distant)
        const wallGeometry = new THREE.PlaneGeometry(200, 50);
        const wallMaterial = new THREE.MeshStandardMaterial({
            color: 0x1a1a2e,
            transparent: true,
            opacity: 0.3,
            side: THREE.DoubleSide
        });
        
        const leftWall = new THREE.Mesh(wallGeometry, wallMaterial);
        leftWall.position.set(-50, 25, -50);
        leftWall.rotation.y = Math.PI / 2;
        this.scene.add(leftWall);
        
        const rightWall = new THREE.Mesh(wallGeometry, wallMaterial);
        rightWall.position.set(50, 25, -50);
        rightWall.rotation.y = -Math.PI / 2;
        this.scene.add(rightWall);
        
        // Ceiling
        const ceilingGeometry = new THREE.PlaneGeometry(200, 200);
        const ceilingMaterial = new THREE.MeshStandardMaterial({
            color: 0x0f0f1a,
            transparent: true,
            opacity: 0.5,
            side: THREE.DoubleSide
        });
        
        const ceiling = new THREE.Mesh(ceilingGeometry, ceilingMaterial);
        ceiling.position.set(0, 40, -50);
        ceiling.rotation.x = Math.PI / 2;
        this.scene.add(ceiling);
    }
    
    update(deltaTime, shipPosition) {
        const time = Date.now() * 0.001;
        
        // Rotate asteroids
        for (const asteroid of this.asteroids) {
            asteroid.rotation.x += asteroid.userData.rotationSpeed.x * deltaTime;
            asteroid.rotation.y += asteroid.userData.rotationSpeed.y * deltaTime;
            asteroid.rotation.z += asteroid.userData.rotationSpeed.z * deltaTime;
        }
        
        // Animate energy cells
        for (const cell of this.energyCells) {
            cell.rotation.y += cell.userData.rotationSpeed * deltaTime;
            cell.userData.shell.rotation.x -= cell.userData.rotationSpeed * 0.5 * deltaTime;
            
            // Pulse effect
            const pulse = Math.sin(time * 3 + cell.userData.pulsePhase) * 0.2 + 1;
            cell.userData.core.scale.setScalar(pulse);
        }
        
        // Animate docking ring
        if (this.dockingRing) {
            this.dockingRing.rotation.z += deltaTime * 0.5;
            
            // Pulse the ring
            const pulse = Math.sin(time * 2) * 0.1 + 1;
            this.dockingRing.children[0].scale.setScalar(pulse);
        }
    }
    
    reset() {
        // Remove all level objects
        for (const asteroid of this.asteroids) {
            this.scene.remove(asteroid);
        }
        this.asteroids = [];
        
        for (const cell of this.energyCells) {
            this.scene.remove(cell);
        }
        this.energyCells = [];
        
        if (this.dockingRing) {
            this.scene.remove(this.dockingRing);
            this.dockingRing = null;
        }
        
        // Recreate level
        this.createLevel();
    }
}
