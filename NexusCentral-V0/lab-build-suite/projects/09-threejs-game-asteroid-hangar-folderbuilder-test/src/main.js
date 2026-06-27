import * as THREE from 'three';
import { Game } from './game/Game.js';

// Initialize the game when DOM is ready
document.addEventListener('DOMContentLoaded', () => {
    const canvas = document.getElementById('game-canvas');
    const game = new Game(canvas);
    
    // Handle window resize
    window.addEventListener('resize', () => {
        game.handleResize();
    });
    
    // Start the game loop
    game.start();
});
