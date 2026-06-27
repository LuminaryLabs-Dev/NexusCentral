import { createSignalFlow } from './signalFlow.js';
import { createInput } from '../game/systems/input.js';
import { createInventory } from '../game/systems/inventory.js';
import { createQuestLog } from '../game/systems/questLog.js';
import { createSaveLoad } from '../game/systems/saveLoad.js';
import { createPlayer } from '../game/entities/player.js';
import { npcs } from '../game/entities/npcs.js';
import { world } from '../game/content/worldData.js';
import { quests } from '../game/content/questData.js';
import { renderHud } from '../ui/hud.js';
import { renderMenu } from '../ui/menu.js';
import { renderTitleScene } from '../game/scenes/TitleScene.js';
import { renderWorldScene } from '../game/scenes/WorldScene.js';
import { renderCombatScene } from '../game/scenes/CombatScene.js';

export class GameApp {
  constructor(mount) {
    this.mount = mount;
    this.player = createPlayer();
    this.inventory = createInventory();
    this.questLog = createQuestLog(quests);
    this.saveLoad = createSaveLoad();
    this.input = createInput(event => this.handleInput(event));
    this.signalFlow = createSignalFlow();
    this.mode = 'boot';
    this.message = 'Booting RPG scaffold...';
  }

  boot() {
    this.input.bind();
    this.signalFlow.record('boot');
    this.showTitle();
  }

  showTitle() {
    this.mode = 'title';
    this.signalFlow.record('title/menu');
    this.render();
  }

  newGame() {
    this.mode = 'world';
    this.player.position = { x: 5, y: 5 };
    this.message = 'New game started. Move with WASD or arrow keys.';
    this.signalFlow.record('new game');
    this.signalFlow.record('world map');
    this.render();
  }

  handleInput(event) {
    if (this.mode !== 'world') return;
    if (event.type === 'move') {
      this.player.position.x += event.dx;
      this.player.position.y += event.dy;
      this.signalFlow.record('player movement');
      this.message = `Moved to ${this.player.position.x}, ${this.player.position.y}.`;
      this.render();
    }
  }

  talkToNpc() {
    const elder = npcs[0];
    this.message = `${elder.name}: ${elder.dialogue}`;
    this.questLog.activate('first-light');
    this.inventory.add('Village Token');
    this.signalFlow.record('NPC interaction');
    this.signalFlow.record('quest log update');
    this.signalFlow.record('inventory update');
    this.render();
  }

  startCombat() {
    this.mode = 'combat';
    this.signalFlow.record('combat encounter placeholder');
    this.message = 'Combat placeholder reached. Future work adds tactics, enemies, and rewards.';
    this.render();
  }

  saveGame() {
    this.saveLoad.save({ player: this.player, inventory: this.inventory.items, quests: this.questLog.snapshot() });
    this.signalFlow.record('save/load placeholder');
    this.message = 'Save placeholder wrote local state.';
    this.render();
  }

  loadGame() {
    const state = this.saveLoad.load();
    this.message = state ? 'Load placeholder recovered local state.' : 'No save placeholder exists yet.';
    this.signalFlow.record('save/load placeholder');
    this.render();
  }

  render() {
    const shell = document.createElement('div');
    shell.className = 'game-shell';
    shell.appendChild(renderHud(this.player, this.inventory, this.questLog, this.signalFlow));
    if (this.mode === 'title') {
      shell.appendChild(renderTitleScene(() => this.newGame()));
    } else if (this.mode === 'combat') {
      shell.appendChild(renderCombatScene(() => { this.mode = 'world'; this.render(); }));
    } else {
      shell.appendChild(renderWorldScene(world, this.player, this.message));
      shell.appendChild(renderMenu({
        talk: () => this.talkToNpc(),
        combat: () => this.startCombat(),
        save: () => this.saveGame(),
        load: () => this.loadGame(),
        menu: () => this.showTitle()
      }));
    }
    this.mount.replaceChildren(shell);
  }
}
