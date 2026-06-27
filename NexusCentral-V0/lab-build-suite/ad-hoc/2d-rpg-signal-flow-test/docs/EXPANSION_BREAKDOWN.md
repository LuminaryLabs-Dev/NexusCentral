# Expansion And Breakdown

## Expansion Pass
- Content pillars: overworld traversal, NPC dialogue, quest state, inventory rewards, combat placeholder, save/load hooks.
- Ten-hour runway: add regions under `worldData`, quest arcs under `questData`, NPC behavior under `npcs`, and encounter templates under `CombatScene`.
- Runtime signal chain: boot -> title/menu -> new game -> world map -> player movement -> NPC interaction -> quest log -> inventory -> combat placeholder -> save/load -> return to menu.

## Breakdown Pass
- `core` owns app lifecycle and signal orchestration.
- `scenes` own visible mode changes.
- `systems` own persistent state transitions.
- `entities` own player/NPC data.
- `content` owns expandable game data.
- `ui` owns HUD/menu rendering.
