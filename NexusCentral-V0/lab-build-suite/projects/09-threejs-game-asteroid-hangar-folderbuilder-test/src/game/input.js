export class InputHandler {
    constructor() {
        this.forward = false;
        this.backward = false;
        this.left = false;
        this.right = false;
        this.boost = false;
        this.pausePressed = false;
        
        this.setupKeyListeners();
    }
    
    setupKeyListeners() {
        document.addEventListener('keydown', (e) => {
            this.handleKeyDown(e);
        });
        
        document.addEventListener('keyup', (e) => {
            this.handleKeyUp(e);
        });
    }
    
    handleKeyDown(e) {
        switch (e.code) {
            case 'KeyW':
            case 'ArrowUp':
                this.forward = true;
                break;
            case 'KeyS':
            case 'ArrowDown':
                this.backward = true;
                break;
            case 'KeyA':
            case 'ArrowLeft':
                this.left = true;
                break;
            case 'KeyD':
            case 'ArrowRight':
                this.right = true;
                break;
            case 'Space':
                this.boost = true;
                e.preventDefault();
                break;
            case 'KeyP':
            case 'Escape':
                this.pausePressed = true;
                e.preventDefault();
                break;
        }
    }
    
    handleKeyUp(e) {
        switch (e.code) {
            case 'KeyW':
            case 'ArrowUp':
                this.forward = false;
                break;
            case 'KeyS':
            case 'ArrowDown':
                this.backward = false;
                break;
            case 'KeyA':
            case 'ArrowLeft':
                this.left = false;
                break;
            case 'KeyD':
            case 'ArrowRight':
                this.right = false;
                break;
            case 'Space':
                this.boost = false;
                break;
        }
    }
}
