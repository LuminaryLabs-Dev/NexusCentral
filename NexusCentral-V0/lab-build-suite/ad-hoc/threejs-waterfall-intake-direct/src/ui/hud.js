/**
 * HUD Manager
 * Handles DOM manipulation for UI elements (Score, Screens, etc).
 */
export default class HUDManager {
    constructor() {
        // Elements
        this.hudEl = document.getElementById('hud');
        this.scoreEl = document.getElementById('score-val');
        this.speedEl = document.getElementById('speed-val');
        
        this.startScreen = document.getElementById('start-screen');
        this.gameOverScreen = document.getElementById('game-over-screen');
        this.finalScoreEl = document.getElementById('final-score');
        this.finalDistanceEl = document.getElementById('final-distance');

        // Buttons
        this.startBtn = document.getElementById('start-btn');
        this.restartBtn = document.getElementById('restart-btn');

        // Callbacks
        this.onStartCallback = null;
        this.onRestartCallback = null;

        this.bindEvents();
    }

    bindEvents() {
        this.startBtn.addEventListener('click', () => {
            if (this.onStartCallback) this.onStartCallback();
        });

        this.restartBtn.addEventListener('click', () => {
            if (this.onRestartCallback) this.onRestartCallback();
        });
    }

    onStart(cb) {
        this.onStartCallback = cb;
    }

    onRestart(cb) {
        this.onRestartCallback = cb;
    }

    showStartScreen() {
        this.startScreen.classList.remove('hidden');
        this.startScreen.classList.add('active');
        this.gameOverScreen.classList.add('hidden');
        this.gameOverScreen.classList.remove('active');
        this.hudEl.classList.add('hidden');
    }

    startGame() {
        this.startScreen.classList.remove('active');
        this.startScreen.classList.add('hidden');
        this.gameOverScreen.classList.remove('active');
        this.gameOverScreen.classList.add('hidden');
        this.hudEl.classList.remove('hidden');
    }

    showGameOver(score, distance) {
        this.hudEl.classList.add('hidden');
        this.gameOverScreen.classList.remove('hidden');
        this.gameOverScreen.classList.add('active');
        
        this.finalScoreEl.innerText = score;
        this.finalDistanceEl.innerText = distance + 'm';
    }

    updateScore(score) {
        this.scoreEl.innerText = score.toLocaleString();
    }

    updateSpeed(speedPercent) {
        this.speedEl.innerText = speedPercent + '%';
        
        // Visual feedback for high speed
        if (speedPercent > 150) {
            this.speedEl.style.color = '#ff0055';
        } else {
            this.speedEl.style.color = '#00f3ff';
        }
    }
}
