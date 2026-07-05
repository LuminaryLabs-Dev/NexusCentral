# Use-Case Catalog

Status: active

## Purpose

Keep the original idea catalog and the richer profile pack in `.agent/` so NexusCentral runtime state stays separate from exploration work.

## Files

- `original-buildable-apps-catalog.json`: original catalog source with 1200 unique app concepts.
- `generate_original_idea_profile_pack.py`: batch tool that expands each catalog item into a full profile.
- `original-idea-profile-pack.json`: generated batch profile pack with 11-step sprint plans and master seed prompts.
- `export_profile_submission_bundle.py`: exporter that packages the full pack or a filtered subset into a one-go submission bundle.
- `profile-submission-bundle.json`: generated submission bundle with the selected profiles.
- `profile-submission-bundle.md`: compact handoff note for the bundle.

## Usage

- Run `python3 .agent/use-case-catalog/generate_original_idea_profile_pack.py` to regenerate the full profile pack.
- Run `python3 .agent/use-case-catalog/export_profile_submission_bundle.py` to produce the maximal submission bundle.
- Use the generated pack when you want to submit as much of an idea profile as possible in one go.
- Keep new catalog experiments under `.agent/use-case-catalog/` instead of `NexusCentral-V1`.

## Profile Contract

Each profile keeps:

- `specificWorld`
- `originalAngle`
- `whyOriginal`
- `primaryWorkflow`
- `signatureInteraction`
- `buildableSlice`
- `nexusCentralValidation`
- `buildRisk`
- `sprintPlan`
- `seedPrompt`
- `submissionEnvelope`

The sprint plan is the build order. The seed prompt is the master spec text that should let a builder start with the whole brief instead of a sketch.
