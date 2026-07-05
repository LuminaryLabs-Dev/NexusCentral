# NexusCentral Report

## Executive Summary

Intent: Audit and brainstorm this repository..
405 files were indexed across 8 inferred domains for this Unity project.
Detected project type: unity (medium confidence).
Unity profile: 146 C# scripts, 0 scenes, 0 prefabs, package manifest missing, ProjectSettings missing.
9 findings were produced; highest severity is blocked.
Decision: Prioritize failing or risky audit findings, then use brainstorm branches to choose the smallest modular next step.

## Repository Profile

Root: ../target
Domains: docs, frontend, repo-intelligence, tests, toolchain, unity, unity-project, unity-scripts
Detected project root: .
Relative roots: Packages.
Profile markers: Packages/, *.cs.
Profile roots: .github, NexusCentral-V0, NexusCentral-V1, README.md, global.json, packages, site, updates.
Source extensions: .cs.
Source files: 146; tests: 12; total lines: 71754.
Unity layout: Assets=no, Packages=yes, ProjectSettings=no.
Unity counts: C# scripts=146, scenes=0, prefabs=0, asmdefs=0.

## Audit Findings

BLOCKED: Unity project is missing an Assets root (unity.missing-assets-root) -> Run the audit from the Unity project root so Assets/, Packages/, and ProjectSettings/ are visible.
RISK: Unity package manifest is missing (unity.missing-package-manifest) -> Include Packages/manifest.json so package dependencies can be audited.
RISK: Unity ProjectSettings are not visible (unity.missing-project-settings) -> Include ProjectSettings/ in source control and the audit snapshot.
WARN: Intent memory is not tracked (docs.missing-memory) -> Add a memory.md ledger for intent, findings, and decisions.
WARN: Large module may hide multiple responsibilities (modularity.large-module) -> Split .github/scripts/update_pages_downloads.py around a stable public contract.
WARN: Large module may hide multiple responsibilities (modularity.large-module) -> Split NexusCentral-V0/Desktop/config/wrappers/nexusagent.json around a stable public contract.
WARN: Large module may hide multiple responsibilities (modularity.large-module) -> Split NexusCentral-V0/Desktop/Services/AppPathsService.cs around a stable public contract.
WARN: Test surface is thin relative to source size (tests.thin-coverage) -> Add focused contract tests for the highest-risk source modules.
WARN: Unity scripts lack visible assembly definition boundaries (unity.missing-assembly-boundaries) -> Add asmdef boundaries for runtime, editor, and tests where ownership is currently implicit.

## Brainstorm Branches

Unity Project Shape: If the Unity project shape is explicit, the audit can distinguish gameplay/runtime risk from package, setting, and asset-pipeline risk. Actions: Inspect unity-project evidence before proposing changes. | Define the smallest Unity audit check for unity project shape. | Run the audit from the Unity project root so Assets/, Packages/, and ProjectSettings/ are visible.
Unity Runtime Hardening: If runtime scripts and scenes have focused hardening checks, gameplay regressions become visible before asset or UI work expands. Actions: Inspect unity-scripts evidence before proposing changes. | Define the smallest Unity audit check for unity runtime hardening. | Include Packages/manifest.json so package dependencies can be audited.
Unity Package And Settings Review: If packages and project settings are audited together, environment drift and missing setup steps are easier to catch. Actions: Inspect unity-packages evidence before proposing changes. | Define the smallest Unity audit check for unity package and settings review. | Run the audit from the Unity project root so Assets/, Packages/, and ProjectSettings/ are visible.
Unity Test Surface: If Unity Test Framework coverage starts at the riskiest scripts or scene transitions, future changes have a concrete regression signal. Actions: Inspect unity-settings evidence before proposing changes. | Define the smallest Unity audit check for unity test surface. | Include Packages/manifest.json so package dependencies can be audited.

## Scout Focus

```markdown
# Project Map
- **Unity Root Candidate**: `..` (invalid — missing `Assets/`, `ProjectSettings/`, `Packages/manifest.json`)
- **Core Domain**: `NexusCentral-V0/Desktop/` — contains 146 source files, dominated by Services (42 files), ViewModels (15 files), and config/wrappers
- **CI/CD**: `.github/workflows/` — 4 YAML pipelines; `.github/scripts/` — one large Python script
- **Test Surface**: Only 12 test files — negligible coverage against 146 source modules
- **Modularity Hotspots**: 
  - `Services/` — 18 files > 300 lines, 6 files > 800 lines
  - `ViewModels/` — 5 files > 400 lines, 1 file (ChatViewModel.cs) at 4665 lines
  - `config/wrappers/nexusagent.json` — 351 lines (JSON blob)
- **Missing Unity Artifacts**: No `Assets/`, `ProjectSettings/`, or `Packages/manifest.json` — **not a valid Unity project snapshot**
# Highest Signal Files

## Alignment Matrix

unity-project-shape: evidence-backed; domain unity-project; findings unity.missing-assets-root, unity.missing-package-manifest.
unity-runtime-hardening: evidence-backed; domain unity-scripts; findings unity.missing-assets-root, unity.missing-package-manifest.
unity-package-settings: evidence-backed; domain unity-packages; findings unity.missing-assets-root, unity.missing-package-manifest.
unity-test-surface: evidence-backed; domain unity-settings; findings unity.missing-assets-root, unity.missing-package-manifest.

## Reconciled Plan

action-1: Run the audit from the Unity project root so Assets/, Packages/, and ProjectSettings/ are visible.
action-2: Include Packages/manifest.json so package dependencies can be audited.
action-3: Include ProjectSettings/ in source control and the audit snapshot.
action-4: Add a memory.md ledger for intent, findings, and decisions.
action-5: Split .github/scripts/update_pages_downloads.py around a stable public contract.
action-6: Split NexusCentral-V0/Desktop/config/wrappers/nexusagent.json around a stable public contract.
action-7: Split NexusCentral-V0/Desktop/Services/AppPathsService.cs around a stable public contract.
action-8: Add focused contract tests for the highest-risk source modules.

## Memory Ledger

Current intent: Audit and brainstorm this repository.
Tracked findings: 9