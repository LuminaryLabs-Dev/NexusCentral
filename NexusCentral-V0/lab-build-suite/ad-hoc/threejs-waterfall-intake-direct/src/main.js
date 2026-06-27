import Game from './game/Game.js';

/**
 * Application Entry Point
 * Initializes the game instance and handles global window events.
 */
function init() {
    const game = new Game();
    
    // Expose game to window for debugging if needed, though not strictly necessary
    window.game = game;

    // Handle window resizing
    window.addEventListener('resize', () => {
        game.onWindowResize();
    });

    // Start the boot sequence
    game.boot();
}

// Wait for DOM to be ready
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
} else {
    init();
}
