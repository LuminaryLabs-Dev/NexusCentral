# Swim Museum Deck Template

Static HTML presentation template for a Three.js and CSS3D "swim-through museum" deck with infinite looping navigation.

## Files

- `index.html`: minimal app shell and overlay UI
- `styles.css`: visual system and slide layout styles
- `script.js`: Three.js scene setup, recycled slide-pool logic, and interaction handling
- `slides.js`: ordered slide data array and sample content
- `assets/`: optional local images or supporting media

## Authoring Model

Slides are authored in `slides.js` as ordered objects. The required baseline fields are:

- `id`
- `variant`: `hero` or `content`
- `sectionLabel`
- `title`
- `status`: `current` or `target`
- `layout`: `stacked`, `grid`, or `split`
- `tiles`: array of tile objects with `title`, optional `body`, and optional `bullets`
- `visual`: optional object with `type` of `placeholder`, `image`, or `mermaid`
- `cta`: optional object for hero-slide actions

The sample deck also shows optional hero helpers such as `subtitle`, `lead`, and `systemDiagram`.

## Infinite Swim-Through

The template does not create one DOM node per slide in the corridor. Instead it keeps a fixed pool of seven CSS3D slide nodes:

1. `targetProgress` tracks the unbounded scroll position.
2. `actualProgress` eases toward the target for smooth movement.
3. `Math.trunc(actualProgress)` defines the active virtual slide anchor.
4. Each slot maps to `virtualIndex = anchorIndex + offset`.
5. The displayed slide becomes `slides[((virtualIndex % slides.length) + slides.length) % slides.length]`.
6. Slots rerender only when their mapped content changes.

This keeps DOM size constant while still allowing seamless forward or backward looping.

## Mermaid And CTA Notes

- Mermaid diagrams are rendered after a recycled slot receives new content; they do not rely on one-time page-load initialization.
- CTA interactions are delegated through data attributes so recycled slides keep working without inline `onclick`.

## Local Preview

Serve the directory with any static web server. Example:

```bash
cd ../../../templates/presentation_templates/swim_museum_deck
python3 -m http.server 8123
```

Then open [http://127.0.0.1:8123](http://127.0.0.1:8123).
