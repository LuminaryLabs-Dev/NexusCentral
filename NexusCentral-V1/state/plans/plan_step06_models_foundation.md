# Step 06: Models Foundation

## One Sentence

Add the Models foundation for endpoint records, provider config, and real health checks.

## State Observes

- Models UI is static.
- Provider adapter folders exist but only contain `.gitkeep`.
- Add/import/set-active controls are disabled.
- Model/provider rows imply support that is not implemented.
- Architecture expects model catalog, provider endpoints, local runtime state, admission, and health.

## Exact Changes To Plan

- Add model endpoint contracts:
  - endpoint id
  - provider kind
  - base URL
  - auth profile reference
  - capabilities
  - health status
  - last probe
- Add provider kinds:
  - LM Studio
  - OpenAI-compatible
  - OpenAI
  - Claude
  - OpenRouter
  - Nvidia
  - local runtime placeholder
- Implement LM Studio/OpenAI-compatible health probe first.
- Add endpoint registry persistence.
- Bind Models UI to endpoint registry.
- Show Hugging Face download as planned until downloader and storage policy exist.

## Hero Controls / User-Facing Impact

- Add endpoint.
- Probe endpoint health.
- Select active endpoint/model if healthy.

## Advanced Controls / Deferred

- Hugging Face downloads.
- Local runtime loading.
- Memory reservation/admission.
- Provider SDK dependencies.
- Raw secret editing in route/model targets.

## Validation

- Add LM Studio-compatible endpoint.
- Probe health without storing raw key in endpoint target.
- Models tab displays real health state.
- Shell Model status reflects selected endpoint or inactive state.
- Build and tests pass.

## Done Criteria

- Models tab is no longer a static mock surface.
- Endpoint health is real for at least one provider class.
- Provider folders represent actual adapter boundaries or are clearly planned.
