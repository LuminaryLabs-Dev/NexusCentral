export function createQuestLog(quests) {
  const active = new Set();
  return {
    activate(id) {
      active.add(id);
    },
    list() {
      return quests.filter(quest => active.has(quest.id));
    },
    snapshot() {
      return Array.from(active);
    }
  };
}
