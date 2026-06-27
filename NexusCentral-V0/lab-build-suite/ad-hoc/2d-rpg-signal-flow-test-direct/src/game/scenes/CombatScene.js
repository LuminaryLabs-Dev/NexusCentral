export function renderCombatScene(onReturn) {
  const section = document.createElement('section');
  section.className = 'scene combat-scene';
  section.innerHTML = `
    <h2>Combat Placeholder</h2>
    <p>Encounter hooks compile now. Future passes add enemy AI, abilities, rewards, and defeat states.</p>
  `;
  const button = document.createElement('button');
  button.textContent = 'Return To World';
  button.addEventListener('click', onReturn);
  section.appendChild(button);
  return section;
}
