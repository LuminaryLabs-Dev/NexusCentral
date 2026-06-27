export function renderMenu(actions) {
  const menu = document.createElement('nav');
  menu.className = 'action-menu';
  const buttons = [
    ['Talk To NPC', actions.talk],
    ['Combat Placeholder', actions.combat],
    ['Save', actions.save],
    ['Load', actions.load],
    ['Return To Menu', actions.menu]
  ];
  for (const [label, handler] of buttons) {
    const button = document.createElement('button');
    button.textContent = label;
    button.addEventListener('click', handler);
    menu.appendChild(button);
  }
  return menu;
}
