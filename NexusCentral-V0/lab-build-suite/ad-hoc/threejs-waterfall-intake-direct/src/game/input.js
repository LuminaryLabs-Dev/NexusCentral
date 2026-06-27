/**
 * Input Manager
 * Handles keyboard input for the game.
 */
export default class InputManager {
    constructor() {
        this.isLeftPressed = false;
        this.isRightPressed = false;
        
        this.bindEvents();
    }

    bindEvents() {
        window.addEventListener('keydown', (e) => this.onKeyDown(e));
        window.addEventListener('keyup', (e) => this.onKeyUp(e));
    }

    onKeyDown(e) {
        switch(e.code) {
            case 'ArrowLeft':
            case 'KeyA':
                this.isLeftPressed = true;
                break;
            case 'ArrowRight':
            case 'KeyD':
                this.isRightPressed = true;
                break;
        }
    }

    onKeyUp(e) {
        switch(e.code) {
            case 'ArrowLeft':
            case 'KeyA':
                this.isLeftPressed = false;
                break;
            case 'ArrowRight':
            case 'KeyD':
                this.isRightPressed = false;
                break;
        }
    }
}
