export class HUD {
    constructor() {
        this.scoreElement = document.getElementById('score');
        this.energyFillElement = document.getElementById('energy-fill');
        this.progressElement = document.getElementById('progress');
        this.gameMessage = document.getElementById('game-message');
        this.messageTitle = document.getElementById('message-title');
        this.messageText = document.getElementById('message-text');
        this.messageButton = document.getElementById('message-button');
        this.pauseOverlay = document.getElementById('pause-overlay');
    }
    
    updateScore(score) {
        this.scoreElement.textContent = score.toString();
    }
    
    updateEnergy(current, max) {
        const percentage = (current / max) * 100;
        this.energyFillElement.style.width = `${percentage}%`;
        
        // Change color based on energy level
        if (percentage < 25) {
            this.energyFillElement.style.backgroundColor = '#ff4444';
        } else if (percentage < 50) {
            this.energyFillElement.style.backgroundColor = '#ffaa00';
        } else {
            this.energyFillElement.style.backgroundColor = '#00ff88';
        }
    }
    
    updateProgress(current, max) {
        const percentage = Math.round((current / max) * 100);
        this.progressElement.textContent = `${percentage}%`;
    }
    
    showMessage(title, text, buttonText, callback) {
        this.messageTitle.textContent = title;
        this.messageText.textContent = text;
        this.messageButton.textContent = buttonText;
        
        // Remove old event listener
        const newButton = this.messageButton.cloneNode(true);
        this.messageButton.parentNode.replaceChild(newButton, this.messageButton);
        this.messageButton = newButton;
        
        // Add new event listener
        this.messageButton.addEventListener('click', callback);
        
        this.gameMessage.classList.remove('hidden');
    }
    
    hideMessage() {
        this.gameMessage.classList.add('hidden');
    }
    
    showPause() {
        this.pauseOverlay.classList.remove('hidden');
    }
    
    hidePause() {
        this.pauseOverlay.classList.add('hidden');
    }
}
