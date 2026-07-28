const glContainer = document.getElementById("gl-container");
const cssContainer = document.getElementById("css-container");
const uiLayer = document.getElementById("ui-layer");
const currentSlideNumber = document.getElementById("current-slide-number");
const totalSlideCount = document.getElementById("total-slide-count");
const loopCount = document.getElementById("loop-count");
const slides = window.swimMuseumSlides || [];

const glScene = new THREE.Scene();
glScene.fog = new THREE.FogExp2(0x020408, 0.0003);

const cssScene = new THREE.Scene();
const camera = new THREE.PerspectiveCamera(45, window.innerWidth / window.innerHeight, 1, 30000);

const glRenderer = new THREE.WebGLRenderer({ alpha: true, antialias: true });
glRenderer.setPixelRatio(window.devicePixelRatio);
glRenderer.setSize(window.innerWidth, window.innerHeight);
glContainer.appendChild(glRenderer.domElement);

const cssRenderer = new THREE.CSS3DRenderer();
cssRenderer.setSize(window.innerWidth, window.innerHeight);
cssContainer.appendChild(cssRenderer.domElement);

const Z_SPACING = 3000;
const START_CAMERA_Z = 1200;
const POOL_RADIUS = 3;
const SLOT_COUNT = POOL_RADIUS * 2 + 1;
const SCROLL_SENSITIVITY = 1 / 1200;
const TOUCH_SENSITIVITY = 1 / 650;

let targetProgress = 0;
let actualProgress = 0;
let activeAnchorIndex = Number.NaN;
let touchStartY = 0;
let mouseX = 0;
let mouseY = 0;
let mermaidRenderCount = 0;

totalSlideCount.textContent = String(slides.length).padStart(2, "0");

mermaid.initialize({
    startOnLoad: false,
    theme: "dark",
    themeVariables: {
        primaryColor: "#101a2b",
        primaryTextColor: "#ffffff",
        primaryBorderColor: "#1bd3d6",
        lineColor: "#1bd3d6",
        secondaryColor: "#0a1320",
        tertiaryColor: "#050b14",
        fontFamily: "'Plus Jakarta Sans', sans-serif"
    }
});

const gridHelper = new THREE.GridHelper(10000, 100, 0x1bd3d6, 0x0a1320);
gridHelper.position.y = -600;
gridHelper.material.opacity = 0.3;
gridHelper.material.transparent = true;
glScene.add(gridHelper);

const particlesGeometry = new THREE.BufferGeometry();
const particleCount = 2000;
const positions = new Float32Array(particleCount * 3);

for (let index = 0; index < particleCount * 3; index += 3) {
    positions[index] = (Math.random() - 0.5) * 10000;
    positions[index + 1] = (Math.random() - 0.5) * 6000;
    positions[index + 2] = (Math.random() - 0.5) * 18000;
}

particlesGeometry.setAttribute("position", new THREE.BufferAttribute(positions, 3));

const particlesMaterial = new THREE.PointsMaterial({
    size: 15,
    color: 0x1bd3d6,
    transparent: true,
    opacity: 0.6,
    blending: THREE.AdditiveBlending
});

const particlesMesh = new THREE.Points(particlesGeometry, particlesMaterial);
glScene.add(particlesMesh);

const slotOffsets = Array.from({ length: SLOT_COUNT }, (_, index) => index - POOL_RADIUS);
const slots = slotOffsets.map((offset, slotIndex) => createSlot(slotIndex, offset));

updateVisibleSlides(0);
updateDeckStatus();

window.addEventListener("wheel", (event) => {
    event.preventDefault();
    handleScroll(event.deltaY * SCROLL_SENSITIVITY);
}, { passive: false });

window.addEventListener("touchstart", (event) => {
    touchStartY = event.touches[0].clientY;
}, { passive: false });

window.addEventListener("touchmove", (event) => {
    event.preventDefault();
    const touchY = event.touches[0].clientY;
    const deltaY = touchStartY - touchY;
    touchStartY = touchY;
    handleScroll(deltaY * TOUCH_SENSITIVITY);
}, { passive: false });

window.addEventListener("mousemove", (event) => {
    mouseX = (event.clientX / window.innerWidth) * 2 - 1;
    mouseY = -(event.clientY / window.innerHeight) * 2 + 1;
});

window.addEventListener("resize", onWindowResize);

cssContainer.addEventListener("click", (event) => {
    const trigger = event.target.closest("[data-cta-type]");

    if (!trigger) {
        return;
    }

    const type = trigger.dataset.ctaType;

    if (type === "alert") {
        window.alert(trigger.dataset.ctaMessage || "Action triggered");
        return;
    }

    if (type === "link" && trigger.dataset.ctaHref) {
        window.open(trigger.dataset.ctaHref, trigger.dataset.ctaTarget || "_blank", "noopener");
    }
});

const clock = new THREE.Clock();
animate();

function createSlot(slotIndex, offset) {
    const element = document.createElement("article");
    element.className = "slide-container";
    element.dataset.slotIndex = String(slotIndex);
    element.setAttribute("role", "group");

    const cssObject = new THREE.CSS3DObject(element);
    cssScene.add(cssObject);

    const frameGeometry = new THREE.PlaneGeometry(1300, 900);
    const haloMaterial = new THREE.MeshBasicMaterial({
        color: 0x000000,
        opacity: 0,
        transparent: true,
        side: THREE.DoubleSide
    });
    const halo = new THREE.Mesh(frameGeometry, haloMaterial);
    glScene.add(halo);

    const frame = new THREE.LineSegments(
        new THREE.EdgesGeometry(frameGeometry),
        new THREE.LineBasicMaterial({ color: 0x1bd3d6, transparent: true, opacity: 0.15 })
    );
    glScene.add(frame);

    const light = new THREE.PointLight(0x1bd3d6, 2, 1500);
    glScene.add(light);

    return {
        offset,
        element,
        cssObject,
        halo,
        frame,
        light,
        lastVirtualIndex: null,
        lastContentIndex: null
    };
}

function handleScroll(deltaProgress) {
    targetProgress += deltaProgress;
    uiLayer.classList.add("is-hidden");
}

function updateVisibleSlides(anchorIndex) {
    for (const slot of slots) {
        const virtualIndex = anchorIndex + slot.offset;
        const contentIndex = mod(virtualIndex, slides.length);
        const slide = slides[contentIndex];

        slot.cssObject.position.set(0, 0, -slot.offset * Z_SPACING);
        slot.halo.position.copy(slot.cssObject.position);
        slot.halo.position.z -= 5;
        slot.frame.position.copy(slot.halo.position);
        slot.light.position.set(0, -500, slot.cssObject.position.z);

        if (slot.lastVirtualIndex === virtualIndex && slot.lastContentIndex === contentIndex) {
            continue;
        }

        slot.lastVirtualIndex = virtualIndex;
        slot.lastContentIndex = contentIndex;
        renderSlideIntoSlot(slot, slide, virtualIndex, contentIndex);
    }
}

function renderSlideIntoSlot(slot, slide, virtualIndex, contentIndex) {
    slot.element.className = `slide-container ${slide.variant === "hero" ? "is-hero" : ""}`.trim();
    slot.element.dataset.variant = slide.variant;
    slot.element.dataset.slideId = slide.id;
    slot.element.dataset.virtualIndex = String(virtualIndex);
    slot.element.dataset.contentIndex = String(contentIndex);
    slot.element.setAttribute("aria-label", `${slide.title} (${contentIndex + 1} of ${slides.length})`);

    if (slide.variant === "hero") {
        slot.element.innerHTML = renderHeroSlide(slide);
    } else {
        slot.element.innerHTML = renderContentSlide(slide);
    }

    renderMermaidInElement(slot.element);
}

function renderHeroSlide(slide) {
    const systemDiagram = (slide.systemDiagram || [])
        .map((item, index) => `
            ${index > 0 ? '<div class="sys-line" aria-hidden="true"></div>' : ""}
            <div class="sys-box">
                <i class="${item.icon}"></i>
                <span>${item.label}</span>
            </div>
        `)
        .join("");

    const cta = slide.cta
        ? `
            <button
                type="button"
                class="demo-link-btn"
                data-cta-type="${slide.cta.actionType || "alert"}"
                data-cta-message="${escapeAttribute(slide.cta.message || "")}"
                data-cta-href="${escapeAttribute(slide.cta.href || "")}"
                data-cta-target="${escapeAttribute(slide.cta.target || "_blank")}"
            >
                ${slide.cta.icon ? `<i class="${slide.cta.icon}"></i>` : ""}
                <span>${slide.cta.label}</span>
            </button>
        `
        : "";

    return `
        <div class="section-label">${slide.sectionLabel}</div>
        <h1 class="hero-title">${slide.title}</h1>
        ${slide.subtitle ? `<h2 class="hero-subtitle">${slide.subtitle}</h2>` : ""}
        ${slide.lead ? `<p class="hero-lead">${slide.lead}</p>` : ""}
        ${systemDiagram ? `<div class="system-diagram">${systemDiagram}</div>` : ""}
        ${cta}
    `;
}

function renderContentSlide(slide) {
    return `
        <div class="slide-header">
            <div class="section-label">${slide.sectionLabel}</div>
            <h2 class="slide-title">${slide.title}</h2>
            <div class="status-badge ${slide.status === "target" ? "target" : ""}">${slide.status}</div>
        </div>
        <div class="content-area">
            ${renderLayout(slide)}
        </div>
    `;
}

function renderLayout(slide) {
    if (slide.layout === "grid") {
        return `<div class="grid-layout">${slide.tiles.map(renderTile).join("")}</div>`;
    }

    if (slide.layout === "split") {
        return `
            <div class="diagram-split-layout">
                <div class="left-col">${slide.tiles.map(renderTile).join("")}</div>
                <div class="right-col">${renderVisual(slide.visual)}</div>
            </div>
        `;
    }

    return `<div class="stacked-layout">${slide.tiles.map(renderTile).join("")}</div>`;
}

function renderTile(tile) {
    const body = tile.body ? `<p>${tile.body}</p>` : "";
    const bullets = tile.bullets
        ? `<ul>${tile.bullets.map((bullet) => `<li>${bullet}</li>`).join("")}</ul>`
        : "";

    return `
        <section class="tile">
            <h3>${tile.title}</h3>
            ${body}
            ${bullets}
        </section>
    `;
}

function renderVisual(visual) {
    if (!visual) {
        return `
            <div class="visual-wrapper">
                <div class="visual-placeholder">
                    <i class="fa-solid fa-circle-info"></i>
                    <div>No visual configured</div>
                </div>
            </div>
        `;
    }

    if (visual.type === "image") {
        return `
            <div class="visual-wrapper">
                <img src="${visual.src}" alt="${visual.alt || ""}">
            </div>
        `;
    }

    if (visual.type === "mermaid") {
        return `
            <div class="diagram-wrapper">
                <div class="mermaid-host" data-mermaid="${encodeMermaid(visual.code)}"></div>
            </div>
        `;
    }

    return `
        <div class="visual-wrapper">
            <div class="visual-placeholder">
                <i class="${visual.icon || "fa-solid fa-cubes"}"></i>
                <div>${visual.title || "Placeholder Visual"}</div>
                ${visual.note ? `<div class="visual-placeholder-note">${visual.note}</div>` : ""}
            </div>
        </div>
    `;
}

function renderMermaidInElement(element) {
    const hosts = element.querySelectorAll(".mermaid-host[data-mermaid]");

    for (const host of hosts) {
        const definition = decodeMermaid(host.dataset.mermaid || "");
        const renderId = `mermaid-${element.dataset.slotIndex}-${element.dataset.virtualIndex}-${mermaidRenderCount++}`;
        host.dataset.renderToken = renderId;

        mermaid.render(renderId, definition)
            .then(({ svg, bindFunctions }) => {
                if (host.dataset.renderToken !== renderId) {
                    return;
                }

                host.innerHTML = svg;

                if (bindFunctions) {
                    bindFunctions(host);
                }
            })
            .catch((error) => {
                host.innerHTML = `<pre style="color:#ff9191;white-space:pre-wrap;">${escapeHtml(error.message)}</pre>`;
            });
    }
}

function updateDeckStatus() {
    const nearestVirtualIndex = Math.round(actualProgress);
    const slideIndex = mod(nearestVirtualIndex, slides.length) + 1;
    const completedLoops = nearestVirtualIndex >= 0
        ? Math.floor(nearestVirtualIndex / slides.length)
        : Math.ceil(nearestVirtualIndex / slides.length);

    currentSlideNumber.textContent = String(slideIndex).padStart(2, "0");
    loopCount.textContent = `${completedLoops >= 0 ? "+" : "-"}${String(Math.abs(completedLoops)).padStart(2, "0")}`;
}

function onWindowResize() {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    glRenderer.setSize(window.innerWidth, window.innerHeight);
    cssRenderer.setSize(window.innerWidth, window.innerHeight);
}

function animate() {
    requestAnimationFrame(animate);

    const elapsedTime = clock.getElapsedTime();
    actualProgress = THREE.MathUtils.lerp(actualProgress, targetProgress, 0.06);

    const anchorIndex = Math.trunc(actualProgress);
    const localProgress = actualProgress - anchorIndex;

    if (anchorIndex !== activeAnchorIndex) {
        activeAnchorIndex = anchorIndex;
        updateVisibleSlides(anchorIndex);
    }

    updateDeckStatus();

    const targetX = mouseX * 200;
    const targetY = mouseY * 100;
    camera.position.x = THREE.MathUtils.lerp(camera.position.x, targetX, 0.05);
    camera.position.y = THREE.MathUtils.lerp(camera.position.y, targetY, 0.05);
    camera.position.z = START_CAMERA_Z - (localProgress * Z_SPACING);

    camera.lookAt(
        camera.position.x * 0.5,
        camera.position.y * 0.5,
        camera.position.z - 2000
    );

    gridHelper.position.z = (elapsedTime * 100) % 100;
    particlesMesh.rotation.y = elapsedTime * 0.02;

    glRenderer.render(glScene, camera);
    cssRenderer.render(cssScene, camera);
}

function mod(value, divisor) {
    return ((value % divisor) + divisor) % divisor;
}

function encodeMermaid(definition) {
    return btoa(unescape(encodeURIComponent(definition.trim())));
}

function decodeMermaid(encoded) {
    return decodeURIComponent(escape(atob(encoded)));
}

function escapeAttribute(value) {
    return value
        .replaceAll("&", "&amp;")
        .replaceAll("\"", "&quot;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;");
}

function escapeHtml(value) {
    return value
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;");
}
