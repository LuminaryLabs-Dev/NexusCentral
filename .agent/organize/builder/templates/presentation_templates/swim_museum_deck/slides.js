const blueprintSvg = `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 1600 900">
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#07111d" />
      <stop offset="50%" stop-color="#0f2238" />
      <stop offset="100%" stop-color="#05080d" />
    </linearGradient>
    <linearGradient id="glow" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%" stop-color="#1bd3d6" stop-opacity="0.1" />
      <stop offset="50%" stop-color="#1bd3d6" stop-opacity="0.7" />
      <stop offset="100%" stop-color="#1bd3d6" stop-opacity="0.1" />
    </linearGradient>
  </defs>
  <rect width="1600" height="900" fill="url(#bg)" />
  <g opacity="0.12" stroke="#1bd3d6" stroke-width="1">
    <path d="M0 180 H1600" />
    <path d="M0 360 H1600" />
    <path d="M0 540 H1600" />
    <path d="M0 720 H1600" />
    <path d="M200 0 V900" />
    <path d="M400 0 V900" />
    <path d="M600 0 V900" />
    <path d="M800 0 V900" />
    <path d="M1000 0 V900" />
    <path d="M1200 0 V900" />
    <path d="M1400 0 V900" />
  </g>
  <g fill="none" stroke="#1bd3d6" stroke-width="3">
    <rect x="240" y="150" width="300" height="520" rx="18" />
    <rect x="650" y="220" width="360" height="420" rx="18" />
    <rect x="1120" y="120" width="220" height="560" rx="18" />
  </g>
  <g fill="url(#glow)">
    <rect x="210" y="180" width="360" height="8" rx="4" />
    <rect x="620" y="250" width="420" height="8" rx="4" />
    <rect x="1090" y="150" width="280" height="8" rx="4" />
  </g>
  <g fill="none" stroke="#1bd3d6" stroke-width="2" opacity="0.85">
    <path d="M540 410 C660 360 720 360 830 420" />
    <path d="M1008 400 C1090 360 1120 340 1210 280" />
  </g>
  <circle cx="540" cy="410" r="10" fill="#1bd3d6" />
  <circle cx="830" cy="420" r="10" fill="#1bd3d6" />
  <circle cx="1008" cy="400" r="10" fill="#1bd3d6" />
  <circle cx="1210" cy="280" r="10" fill="#1bd3d6" />
  <text x="120" y="820" fill="#8ddfe2" font-size="44" font-family="Plus Jakarta Sans, sans-serif" font-weight="700">
    Enterprise Building Hologram
  </text>
  <text x="122" y="862" fill="#5ab6ba" font-size="24" font-family="Plus Jakarta Sans, sans-serif">
    Reusable local SVG sample for image-based presentation slides
  </text>
</svg>
`;

const blueprintImage = `data:image/svg+xml;charset=UTF-8,${encodeURIComponent(blueprintSvg)}`;

window.swimMuseumSlides = [
    {
        id: "master-overview",
        variant: "hero",
        sectionLabel: "Master Overview",
        title: "ProjectArrowhead",
        status: "current",
        layout: "stacked",
        tiles: [],
        subtitle: "Museum Multiverse",
        lead: "A multi-surface device and marketplace platform. This template turns a presentation deck into a seamless swim-through museum with virtualized slide reuse.",
        systemDiagram: [
            { icon: "fa-solid fa-rocket", label: "Launcher" },
            { icon: "fa-solid fa-globe", label: "Portal" },
            { icon: "fa-solid fa-server", label: "Backend" },
            { icon: "fa-solid fa-database", label: "Storage" }
        ],
        cta: {
            label: "Initialize Demo",
            icon: "fa-solid fa-arrow-up-right-from-square",
            actionType: "alert",
            message: "Simulation Demo Initiated"
        }
    },
    {
        id: "executive-summary",
        variant: "content",
        sectionLabel: "Opening",
        title: "Executive Summary",
        status: "current",
        layout: "grid",
        tiles: [
            {
                title: "Current Foundation",
                bullets: [
                    "Combines device-facing launcher with web control-plane surfaces.",
                    "Centers on org management, device claims, and artifact deployment.",
                    "Live backend is <b>Rails-based API</b> architecture."
                ]
            },
            {
                title: "Intended Direction",
                bullets: [
                    "Platform scale for enterprise marketplace distribution.",
                    "A <b>Rust launcher</b> workspace exists as a future-facing parity track.",
                    "Seamless artifact deployment flows across all device types."
                ]
            }
        ]
    },
    {
        id: "architecture-context",
        variant: "content",
        sectionLabel: "Opening",
        title: "Architecture Context",
        status: "current",
        layout: "stacked",
        tiles: [
            {
                title: "Reading This Deck",
                bullets: [
                    "<b>What Is Intended:</b> Strategic north star for platform surfaces and long-term tech evolution.",
                    "<b>What Exists:</b> Live repository structure and runtime shape deployed in production environments today.",
                    "<b>Removed Elements:</b> Deprecated legacy architectures are excluded to keep the swim-through corridor focused."
                ]
            }
        ]
    },
    {
        id: "target-product-vision",
        variant: "content",
        sectionLabel: "What Is Intended",
        title: "Target Product & Vision",
        status: "target",
        layout: "split",
        tiles: [
            {
                title: "Enterprise Strategy",
                body: "Arrowhead is an enterprise distribution platform managing organizations, cross-reality products, and full device lifecycles under one unified control plane."
            }
        ],
        visual: {
            type: "image",
            src: blueprintImage,
            alt: "Stylized enterprise building hologram"
        }
    },
    {
        id: "security-tenancy",
        variant: "content",
        sectionLabel: "What Is Intended",
        title: "Security & Tenancy Rules",
        status: "target",
        layout: "split",
        tiles: [
            {
                title: "Zero Trust Baseline",
                bullets: [
                    "Server-side organization scoping enforcing strict data isolation for all tenants.",
                    "Device tokens function independently from standard browser session cookies.",
                    "Artifact delivery is secured via ephemeral signed URLs mapped to private storage buckets."
                ]
            }
        ],
        visual: {
            type: "placeholder",
            icon: "fa-solid fa-shield-virus",
            title: "Cyber Security Protocol",
            note: "[Active Scanning...]"
        }
    },
    {
        id: "current-web-stack",
        variant: "content",
        sectionLabel: "What Exists",
        title: "Current Web Stack",
        status: "current",
        layout: "split",
        tiles: [
            {
                title: "Architecture Summary",
                body: "Organized as a high-performance monorepo, utilizing <b>Rails</b> as the central authoritative API behind specialized React client surfaces."
            },
            {
                title: "Core Standards",
                body: "All clients utilize a unified <b>API Bridge</b>.<br>Admin Frontend remains physically isolated.<br>Storage logic is abstracted for scalable parity."
            }
        ],
        visual: {
            type: "mermaid",
            code: `
flowchart TB
    W["Web Workspace\\n(Monorepo)"]
    FE["Frontend:\\nMain Portal"]
    AF["Admin:\\nInternal UI"]
    BE["Backend:\\nRails API"]
    ST["Storage:\\nService"]
    W --> FE
    W --> AF
    W --> BE
    W --> ST
    style W fill:#1bd3d6,stroke:#ffffff,stroke-width:2px,color:#050b14
    classDef default fill:#101a2b,stroke:#1bd3d6,stroke-width:1px,color:#ffffff
`
        }
    },
    {
        id: "rust-workspace-status",
        variant: "content",
        sectionLabel: "What Exists",
        title: "Rust Workspace Status",
        status: "current",
        layout: "split",
        tiles: [
            {
                title: "Development Workspace",
                body: "The <b>Launcher-Rust</b> workspace is a future-facing track designed for high performance and immersive shell integration, existing parallel to production systems."
            },
            {
                title: "Strategic Objectives",
                bullets: [
                    "<b>Architecture:</b> Modular split across specialized internal crates.",
                    "<b>Immersive Ambitions:</b> Integrated <b>3D Shell</b> and OpenXR runtime points."
                ]
            }
        ],
        visual: {
            type: "placeholder",
            icon: "fa-brands fa-rust",
            title: "Rust Runtime Compilation",
            note: "[Native Performance Enabled]"
        }
    },
    {
        id: "runtime-topology",
        variant: "content",
        sectionLabel: "What Exists",
        title: "Current Runtime Topology",
        status: "current",
        layout: "split",
        tiles: [
            {
                title: "Environment Mapping",
                body: "Local dev environments map specific ports via Docker Compose, while production routes natively through managed Cloud SQL and auto-scaling Cloud Run services."
            }
        ],
        visual: {
            type: "mermaid",
            code: `
flowchart LR
    subgraph LOCAL["Local Dev"]
        FE1["frontend :3000"]
        BE1["backend :3001"]
        FE1 --> BE1
    end
    subgraph HOSTED["Hosted Cloud"]
        FE2["Portal App"]
        BE2["Cloud Run"]
        DB2["Cloud SQL"]
        FE2 --> BE2
        BE2 --> DB2
    end
    classDef default fill:#101a2b,stroke:#1bd3d6,stroke-width:1px,color:#ffffff
`
        }
    }
];
