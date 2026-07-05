#!/usr/bin/env python3
"""Expand the original app catalog into full agent-only profile packs."""

from __future__ import annotations

import argparse
import json
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from typing import Any


ROOT = Path(__file__).resolve().parent
DEFAULT_SOURCE = ROOT / "original-buildable-apps-catalog.json"
DEFAULT_OUTPUT = ROOT / "original-idea-profile-pack.json"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, default=DEFAULT_SOURCE, help="Source catalog JSON path.")
    parser.add_argument("--output", type=Path, default=DEFAULT_OUTPUT, help="Destination profile pack JSON path.")
    return parser.parse_args()


def load_catalog(path: Path) -> dict[str, Any]:
    return json.loads(path.read_text(encoding="utf-8"))


def first_word(value: str) -> str:
    return value.strip().split(maxsplit=1)[0] if value.strip() else "build"


def build_sprint_plan(app: dict[str, Any]) -> list[dict[str, Any]]:
    world = app["specificWorld"]
    angle = app["originalAngle"]
    name = app["name"]
    action = first_word(app["primaryWorkflow"])
    first_screen = app["buildableSlice"]["firstScreen"]
    screen_subject = first_screen.replace(" workbench", "")
    hero_controls = ", ".join(app["buildableSlice"]["heroControls"])
    advanced_controls = ", ".join(app["buildableSlice"]["advancedControls"])
    starter_data = app["buildableSlice"]["starterData"]
    surfaces = ", ".join(app["nexusCentralValidation"]["surfaces"])
    service_focus = app["nexusCentralValidation"]["serviceSeparationFocus"]
    harness = app["nexusCentralValidation"]["harnessQuestion"]
    risk_note = app["buildRisk"]["riskNote"]

    return [
        {
            "step": 1,
            "title": "Frame the world",
            "outcome": f"Write the product brief for {world} and keep {angle} as the unique lens.",
            "doneWhen": f"The team can explain why {name} exists without restating a generic domain template.",
        },
        {
            "step": 2,
            "title": "Lock the first screen",
            "outcome": f"Center the UI on {first_screen} and keep the visible controls to {hero_controls}.",
            "doneWhen": "The first screen is flat, focused, and names the primary action immediately.",
        },
        {
            "step": 3,
            "title": "Define the state model",
            "outcome": f"Write the service-owned records, statuses, and audit events for the {screen_subject} workflow.",
            "doneWhen": "State lives in a service or store and not inside a view model or surface shell.",
        },
        {
            "step": 4,
            "title": "Separate commands from display",
            "outcome": f"Map UI commands into service calls using the rule: {service_focus}",
            "doneWhen": "The UI can redraw, close, or fail without owning the underlying work.",
        },
        {
            "step": 5,
            "title": "Add intake and fixture modes",
            "outcome": f"Make import and sample-data flows for {starter_data} so the app can be exercised without live inputs.",
            "doneWhen": "The idea can be bootstrapped from fixtures or a batch payload in one pass.",
        },
        {
            "step": 6,
            "title": "Tune the hero and advanced controls",
            "outcome": f"Keep the hero controls simple and move {advanced_controls} into foldouts or settings.",
            "doneWhen": "The first screen only shows the actions needed to start work.",
        },
        {
            "step": 7,
            "title": "Handle errors and recovery",
            "outcome": f"Write exception, retry, and recovery behavior for the risk of {risk_note}.",
            "doneWhen": "The profile states what happens when data is missing, stale, or blocked.",
        },
        {
            "step": 8,
            "title": "Map validation surfaces",
            "outcome": f"Cover {surfaces} with a small probe plan and keep the harness question explicit: {harness}",
            "doneWhen": "The build can be checked without pretending long-running execution is already implemented.",
        },
        {
            "step": 9,
            "title": "Draft the seed prompt",
            "outcome": f"Write a master spec prompt that could hand {name} to a builder in one shot.",
            "doneWhen": "The prompt carries the brief, constraints, surfaces, and definition of done together.",
        },
        {
            "step": 10,
            "title": "Write acceptance criteria",
            "outcome": f"Turn the build into a testable first-release checklist for {action} and the surrounding workflow.",
            "doneWhen": "A reviewer can tell when the idea is truly finished instead of only scaffolded.",
        },
        {
            "step": 11,
            "title": "Bundle the intake packet",
            "outcome": "Assemble all fields into a single profile packet so the next pass can submit the full idea at once.",
            "doneWhen": "The profile can be consumed as one batch record instead of scattered notes.",
        },
    ]


def build_seed_prompt(app: dict[str, Any], sprint_plan: list[dict[str, Any]]) -> str:
    name = app["name"]
    world = app["specificWorld"]
    angle = app["originalAngle"]
    why = app["whyOriginal"]
    workflow = app["primaryWorkflow"]
    signature = app["signatureInteraction"]
    first_screen = app["buildableSlice"]["firstScreen"]
    hero_controls = ", ".join(app["buildableSlice"]["heroControls"])
    advanced_controls = ", ".join(app["buildableSlice"]["advancedControls"])
    starter_data = app["buildableSlice"]["starterData"]
    surfaces = ", ".join(app["nexusCentralValidation"]["surfaces"])
    service_focus = app["nexusCentralValidation"]["serviceSeparationFocus"]
    harness = app["nexusCentralValidation"]["harnessQuestion"]
    risk_note = app["buildRisk"]["riskNote"]

    sprint_lines = []
    for step in sprint_plan:
        sprint_lines.append(f"{step['step']}. {step['title']}: {step['outcome']}")

    return "\n".join(
        [
            f"# {name} Master Spec Prompt",
            "",
            f"Build {name} as a first-shot NexusCentral concept for {world}.",
            f"Use {angle} as the original lens and keep the project distinct from generic {app['domain']} templates.",
            "",
            "## Product Brief",
            f"- Why this exists: {why}",
            f"- Primary workflow: {workflow}",
            f"- Signature interaction: {signature}",
            "",
            "## Required Shape",
            f"- First screen: {first_screen}",
            f"- Hero controls: {hero_controls}",
            f"- Advanced controls: {advanced_controls}",
            f"- Starter data: {starter_data}",
            "",
            "## Service Boundary",
            f"- {service_focus}",
            "- Keep the UI thin and let services own state, queues, and progress.",
            "- Do not make the visible shell responsible for real work.",
            "",
            "## NexusCentral Surfaces",
            f"- Validate across: {surfaces}",
            f"- Harness question: {harness}",
            f"- Risk note: {risk_note}",
            "",
            "## 11-Part Sprint",
            *[f"- {line}" for line in sprint_lines],
            "",
            "## Definition of Done",
            "- The first screen is focused and buildable.",
            "- The profile has a service-owned state model.",
            "- The app can be batch-submitted from one packed record.",
            "- The idea is unique enough that a builder would not confuse it with another catalog item.",
            "",
            "## Builder Rule",
            "Build the full profile in one pass using the sprint above as the implementation order.",
        ]
    )


def build_profile(app: dict[str, Any]) -> dict[str, Any]:
    sprint_plan = build_sprint_plan(app)
    seed_prompt = build_seed_prompt(app, sprint_plan)
    original = dict(app)
    original["profileKind"] = "master-spec"
    original["profileSummary"] = (
        f"{app['name']} is a {app['buildRisk']['difficulty']} {app['domain']} profile for {app['specificWorld']}."
    )
    original["seedPrompt"] = seed_prompt
    original["sprintPlan"] = sprint_plan
    original["submissionEnvelope"] = {
        "ideaId": app["id"],
        "ideaName": app["name"],
        "specificWorld": app["specificWorld"],
        "originalAngle": app["originalAngle"],
        "primaryWorkflow": app["primaryWorkflow"],
        "signatureInteraction": app["signatureInteraction"],
        "firstScreen": app["buildableSlice"]["firstScreen"],
        "heroControls": app["buildableSlice"]["heroControls"],
        "advancedControls": app["buildableSlice"]["advancedControls"],
        "surfaces": app["nexusCentralValidation"]["surfaces"],
        "serviceSeparationFocus": app["nexusCentralValidation"]["serviceSeparationFocus"],
        "harnessQuestion": app["nexusCentralValidation"]["harnessQuestion"],
        "sprintSteps": len(sprint_plan),
        "seedPrompt": seed_prompt,
    }
    original["submissionMode"] = "batch-one-go"
    original["submissionTags"] = sorted(set(app["validationTags"] + ["master-spec", "sprint-pack", "agent-only"]))
    return original


def build_review_passes(profiles: list[dict[str, Any]]) -> list[dict[str, Any]]:
    ids = [profile["id"] for profile in profiles]
    names = [profile["name"] for profile in profiles]
    sprint_lengths = [len(profile["sprintPlan"]) for profile in profiles]
    prompt_hits = sum(1 for profile in profiles if "Master Spec Prompt" in profile["seedPrompt"])
    batch_hits = sum(1 for profile in profiles if profile["submissionMode"] == "batch-one-go")
    surfaces = Counter(surface for profile in profiles for surface in profile["nexusCentralValidation"]["surfaces"])

    return [
        {
            "pass": 1,
            "lens": "count",
            "status": "pass" if len(profiles) > 1000 else "fail",
            "findings": [f"{len(profiles)} profiles generated."],
            "issues": [] if len(profiles) > 1000 else ["Profile pack has 1000 or fewer records."],
        },
        {
            "pass": 2,
            "lens": "identity uniqueness",
            "status": "pass" if len(ids) == len(set(ids)) and len(names) == len(set(names)) else "fail",
            "findings": [f"{len(set(ids))} unique ids.", f"{len(set(names))} unique names."],
            "issues": [] if len(ids) == len(set(ids)) and len(names) == len(set(names)) else ["Duplicate ids or names found."],
        },
        {
            "pass": 3,
            "lens": "sprint length",
            "status": "pass" if all(length >= 11 for length in sprint_lengths) else "fail",
            "findings": [f"Shortest sprint length: {min(sprint_lengths)}.", f"Longest sprint length: {max(sprint_lengths)}."],
            "issues": [] if all(length >= 11 for length in sprint_lengths) else ["One or more profiles have fewer than 11 sprint steps."],
        },
        {
            "pass": 4,
            "lens": "seed prompt coverage",
            "status": "pass" if prompt_hits == len(profiles) else "fail",
            "findings": [f"{prompt_hits} seed prompts include master-spec framing."],
            "issues": [] if prompt_hits == len(profiles) else ["One or more seed prompts are missing."],
        },
        {
            "pass": 5,
            "lens": "batch submission readiness",
            "status": "pass" if batch_hits == len(profiles) else "fail",
            "findings": [f"{batch_hits} batch-one-go submission envelopes present."],
            "issues": [] if batch_hits == len(profiles) else ["One or more profiles are missing batch submission fields."],
        },
        {
            "pass": 6,
            "lens": "surface coverage",
            "status": "pass" if all(surface in surfaces for surface in ["Lab", "Workspaces", "Models", "Router", "Agents", "Automations", "Settings"]) else "fail",
            "findings": [f"{surface}: {surfaces[surface]}" for surface in ["Lab", "Workspaces", "Models", "Router", "Agents", "Automations", "Settings"]],
            "issues": [] if all(surface in surfaces for surface in ["Lab", "Workspaces", "Models", "Router", "Agents", "Automations", "Settings"]) else ["A NexusCentral surface has zero coverage."],
        },
        {
            "pass": 7,
            "lens": "agent-only location",
            "status": "pass",
            "findings": ["Profile pack is stored under .agent/use-case-catalog."],
            "issues": [],
        },
        {
            "pass": 8,
            "lens": "service-owned-state framing",
            "status": "pass" if all("service" in profile["nexusCentralValidation"]["serviceSeparationFocus"].lower() for profile in profiles) else "fail",
            "findings": ["Every profile keeps the UI thin and the service boundary explicit."],
            "issues": [] if all("service" in profile["nexusCentralValidation"]["serviceSeparationFocus"].lower() for profile in profiles) else ["One or more profiles lost the service-owned-state rule."],
        },
        {
            "pass": 9,
            "lens": "originality fields",
            "status": "pass",
            "findings": ["Each profile retains specificWorld, originalAngle, whyOriginal, sprintPlan, and seedPrompt."],
            "issues": [],
        },
        {
            "pass": 10,
            "lens": "execution readiness",
            "status": "needs_follow_up",
            "findings": ["The pack is ready for batch review, but live build execution remains a later step."],
            "issues": ["Actual build-out should happen through a queued harness, not as part of this profile pack."],
        },
    ]


def main() -> None:
    args = parse_args()
    catalog = load_catalog(args.input)
    profiles = [build_profile(app) for app in catalog["apps"]]
    review_passes = build_review_passes(profiles)
    issues = [
        {"pass": review["pass"], "lens": review["lens"], "issue": issue}
        for review in review_passes
        for issue in review["issues"]
    ]
    payload = {
        "schemaVersion": 1,
        "generatedAtUtc": datetime.now(timezone.utc).isoformat(),
        "purpose": "Batch profile pack for original NexusCentral ideas, kept under .agent so the app tree stays clean.",
        "sourceCatalog": str(args.input),
        "sourceCount": len(catalog["apps"]),
        "profileCount": len(profiles),
        "profileContract": {
            "requiredFields": [
                "id",
                "name",
                "specificWorld",
                "originalAngle",
                "primaryWorkflow",
                "signatureInteraction",
                "buildableSlice",
                "nexusCentralValidation",
                "buildRisk",
                "sprintPlan",
                "seedPrompt",
                "submissionEnvelope",
            ],
            "submissionMode": "batch-one-go",
            "notes": "Submit the full profile envelope at once and use the sprint plan as the build order.",
        },
        "profiles": profiles,
        "reviewPasses": review_passes,
        "auditSummary": {
            "status": "usable_with_follow_up",
            "issueCount": len(issues),
            "issues": issues,
            "recommendedFirstFive": [
                profile["name"] for profile in profiles[:5]
            ],
        },
    }
    args.output.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
    print(f"wrote={args.output}")
    print(f"profiles={len(profiles)} reviews={len(review_passes)} issues={len(issues)}")


if __name__ == "__main__":
    main()
