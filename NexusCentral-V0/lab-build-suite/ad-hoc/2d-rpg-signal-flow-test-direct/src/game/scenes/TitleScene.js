export function renderTitleScene(onStart) {
  const section = document.createElement('section');
  section.className = 'scene title-scene';
  section.innerHTML = `
    <h2>Echoes of the Ten-Hour Vale</h2>
    <p>A scaffolded 2D RPG content runway with expandable regions, quests, inventory, and combat hooks.</p>
  `;
  const button = document.createElement('button');
  button.textContent = 'New Game';
  button.addEventListener('click', onStart);
  section.appendChild(button);
  return section;
}
