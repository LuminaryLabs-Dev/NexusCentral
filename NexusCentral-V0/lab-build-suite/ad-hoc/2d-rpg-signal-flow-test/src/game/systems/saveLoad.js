const SAVE_KEY = 'nexus-rpg-scaffold-save';

export function createSaveLoad() {
  return {
    save(state) {
      localStorage.setItem(SAVE_KEY, JSON.stringify(state));
    },
    load() {
      const raw = localStorage.getItem(SAVE_KEY);
      return raw ? JSON.parse(raw) : null;
    }
  };
}
