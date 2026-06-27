# GLM Harness Reliability Suite

This folder defines the 10 exact build prompts NexusGLMHarness must handle reliably from NexusCentral Web Lab.

The suite is source-controlled input and validation infrastructure. Pack A uses `prompts/` and `projects/`; Pack B uses `prompts-b/` and `projects-b/` for 10 handwritten massive app specs.

## Commands

Validate the prompt and expected-output definitions:

```bash
python3 scripts/lab_build_suite.py check-specs
python3 scripts/lab_build_suite.py --pack b check-specs
python3 scripts/lab_build_suite.py check-specs --pack all
```

Validate existing generated outputs without running builds:

```bash
python3 scripts/lab_build_suite.py validate-projects --write-reports
python3 scripts/lab_build_suite.py validate-projects --pack b --write-reports
```

Run compiler and CLI validation for existing generated outputs:

```bash
python3 scripts/lab_build_suite.py validate-projects --compiler --cli --write-reports
python3 scripts/lab_build_suite.py validate-projects --pack all --compiler --lint --test --cli --write-reports
```

Run the harness for one project:

```bash
python3 scripts/lab_build_suite.py run --slug 01-2d-game-orbit-rescue
python3 scripts/lab_build_suite.py run --pack b --slug 11-local-finance-planner-long
```

Run the full suite:

```bash
python3 scripts/lab_build_suite.py run
python3 scripts/lab_build_suite.py run --pack all
```

## Gates

- `files`: required output files exist and are non-empty.
- `compiler`: `npm install --no-audit --no-fund` and `npm run build`.
- `lint`: `npm run lint` when a lint script is present.
- `test`: `npm test` when a test script is present.
- `cli_validation`: `python3 -m nexus_glm_harness validate --workspace <project>`.
- `human_view_static`: local-only asset policy and required interaction terms are present.
- `long_file`: required line-count stress tests pass.
- `three_dependency`: Three.js prompts include a local `three` package dependency.
