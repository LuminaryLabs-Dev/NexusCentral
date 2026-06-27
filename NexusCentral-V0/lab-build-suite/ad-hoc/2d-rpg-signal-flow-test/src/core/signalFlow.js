export function createSignalFlow() {
  const expected = [
    'boot',
    'title/menu',
    'new game',
    'world map',
    'player movement',
    'NPC interaction',
    'quest log update',
    'inventory update',
    'combat encounter placeholder',
    'save/load placeholder'
  ];
  const events = [];
  return {
    expected,
    events,
    record(event) {
      events.push({ event, at: new Date().toISOString() });
    },
    summary() {
      return expected.map(event => ({ event, seen: events.some(item => item.event === event) }));
    }
  };
}
