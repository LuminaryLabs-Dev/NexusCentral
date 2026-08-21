# Human-View Proof

Mandatory question: Have I checked what the human would actually see, and do I need screenshots, visual inspection, launch-state inspection, or before/after comparison to validate this properly?

Answer: yes. Launch-state inspection, interaction snapshots, full-page screenshots, and artifact download proof were captured.

- `output/playwright/validate-it-lab.png`: Lab launches clearly and shows `Route setup needed`.
- `output/playwright/validate-it-router.png`: Router exposes LM Studio setup but no LFM2-350M default or discovered model.
- `output/playwright/validate-it-models.png`: Models clearly shows no local catalog or runtime.
- `output/playwright/validate-it-review.png`: clean empty Review state.
- `output/playwright/validate-it-settings.png`: service, mesh, harness, pairing, and browser inference controls are visible; service is not installed.
- `output/playwright/validate-it-review-after-run.png`: canceled run, hashed input artifact, download action, rejected harness import, and command receipts are visible.

Human-view result: the existing surfaces are readable and operable, but the target is incomplete. There is no NexusAgent harness upload control, no Workflows surface, no distinct Runs surface, no LFM first-run action, and no detailed route failure explanation. The audit receipt list is long and lacks filtering or error-detail expansion.
