import './styles/main.css';
import { GameApp } from './core/GameApp.js';

const mount = document.querySelector('#app');
const game = new GameApp(mount);
game.boot();
