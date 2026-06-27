export function renderWorldScene(world, player, message) {
  const section = document.createElement('section');
  section.className = 'scene world-scene';
  const map = world.tiles.map(row => `<div class="map-row">${row.map(tile => `<span class="tile">${tile}</span>`).join('')}</div>`).join('');
  section.innerHTML = `
    <h2>${world.name}</h2>
    <div class="map" aria-label="world map">${map}</div>
    <p>Player: ${player.name} at ${player.position.x}, ${player.position.y}</p>
    <p>${message}</p>
  `;
  return section;
}
