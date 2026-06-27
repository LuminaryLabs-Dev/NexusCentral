export function createInput(emit) {
  const map = {
    ArrowUp: { dx: 0, dy: -1 },
    ArrowDown: { dx: 0, dy: 1 },
    ArrowLeft: { dx: -1, dy: 0 },
    ArrowRight: { dx: 1, dy: 0 },
    w: { dx: 0, dy: -1 },
    s: { dx: 0, dy: 1 },
    a: { dx: -1, dy: 0 },
    d: { dx: 1, dy: 0 }
  };
  return {
    bind() {
      window.addEventListener('keydown', event => {
        const move = map[event.key];
        if (move) {
          emit({ type: 'move', ...move });
        }
      });
    }
  };
}
