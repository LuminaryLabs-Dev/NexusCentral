export function renderHud(player, inventory, questLog, signalFlow) {
  const hud = document.createElement('aside');
  hud.className = 'hud';
  const quests = questLog.list().map(quest => `<li>${quest.title}</li>`).join('') || '<li>No active quests</li>';
  const items = inventory.items.map(item => `<li>${item}</li>`).join('') || '<li>Inventory empty</li>';
  const signals = signalFlow.summary().map(item => `<li class="${item.seen ? 'seen' : ''}">${item.seen ? '✓' : '·'} ${item.event}</li>`).join('');
  hud.innerHTML = `
    <h2>RPG Signal Flow</h2>
    <p>${player.name} HP ${player.hp}</p>
    <h3>Quests</h3><ul>${quests}</ul>
    <h3>Inventory</h3><ul>${items}</ul>
    <h3>Signals</h3><ol>${signals}</ol>
  `;
  return hud;
}
