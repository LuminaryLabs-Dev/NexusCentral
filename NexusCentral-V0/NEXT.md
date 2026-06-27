# NEXT: NexusCentral Evolution Roadmap

This document tracks the immediate technical priorities and architectural milestones for NexusCentral-Triceratops.

## 🎯 Immediate Objectives (Active Sprint)
- [ ] **NexusAgent Harness Optimization**
    - Introduce model-authored Markdown patch input as the primary write contract.
    - [x] Implement deterministic patch application plus automated repair retries for failed validation.
    - Improve the direct-answer path to reduce reliance on adapter fallbacks.
    - **Shell Execution Tool**: Introduce a dedicated command-line tool for the agent, strictly scoped to the wrapper's working directory.
    - **Knowledge Traceability**: Refine agent prompts to explicitly explain/cite where knowledge is derived from during each inference step.
- [ ] **UI/UX Consistency**
    - Verify NexusAgent is the prioritized/default selection in all wrapper dropdowns (Task, Workflow, Memory, Artifacts).
    - Ensure "Talk To" dropdown correctly reflects active wrapper states.
    - Batch observable updates to prevent UI stutter during high-token streaming.
    - [x] **Exposed Wrapper Settings**: Implement a horizontal scroll of settings in the top bar (to the right of the dropdown). Only settings explicitly marked as `exposed` should appear; wrapper configs default to exposing none unless a setting opts in.

## 🛠 Architectural Refinement
- [ ] **Runtime Stability**
    - Finalize the BitNet/GGUF adapter handoff logic to ensure stable memory budgets.
    - Implement explicit UI states for `unsupported`, `runtime unavailable`, and `blocked-by-memory`.
- [ ] **Memory & Context**
    - Solidify NexusAgent-owned memory persistence.
    - Ensure Direct LLM correctly maintains `supports_memory=false`.
- [x] **NexusAgent Layers (Internal GPAE Loops)**
    - Implement support for internal sub-loops to solve multi-part goals.
    - **Logic**: Each layer executes its own **GPAE (Goal -> Plan -> Act -> Explain)** loop specifically for its assigned sub-goal.
    - Controlled via a `layers` flag; allows for recursive solving nested within the main wrapper flow.
- [ ] **Modular Flow Orchestration**
    - Refactor agent behaviors (Layers, Review Loops, Expansion) into distinct modules.
    - Modules should dynamically direct the execution flow based on active flags and goals.
- [ ] **Global Orchestration Loop (GPAE)**
    - Implement a high-level **Goal -> Plan -> Act -> Explain** (GPAE) loop.
    - **Execution Control**: Default to 1 iteration, with serialized wrapper settings now available for follow-on numeric inputs.
    - **Integration**: This loop serves as the parent container for all modular behaviors (Layers, Expansion, and Review Pipeline).

## 🧪 Validation & Testing
- [ ] **End-to-End Vite Flow**
    - Verify full project materialization using model-authored patches.
    - Validate build success and browser preview (Port 4179) as part of the "Gate" logic.
- [ ] **Wrapper Catalog Integrity**
    - Automated cleanup of stale user wrappers from `logs/wrappers`.
    - Verification of `direct_llm.json` and `nexusagent.json` in debug builds.

## 🔄 Advanced Review & Validation Pipeline
- [ ] **Multi-Step Recursive Review Loop**
    - Integrate a dedicated "Review and Edit" loop inside the agent's main execution flow.
    - **Step-Based Auditing**: Implement multi-file review cycles for:
        - *Planning Audit*: Verifying architectural alignment.
        - *Security Review*: Scanning for vulnerabilities and directory breakouts.
        - *Debugging/Correctness*: Code quality and logic validation.
        - *Human-View Simulation*: Visual verification and UI layout checks.
    - **Simulation-Driven**: Each step uses computer simulation setups to replicate environments before finalizing changes.
- [ ] **Expansion Factor Setting**
    - Implement a configurable "Expansion Factor" for resolving prompt vagueness:
        - *Factor 1 (Default)*: Simple review and explanation.
        - *Factor 2+*: Recursive extraction loop to identify all granular details, followed by aggregation into an expanded comprehensive list.

---
*Last Updated: 2026-05-02*
