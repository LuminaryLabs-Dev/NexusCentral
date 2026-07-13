# Harness Experiments

## Flow

```text
Lab request
-> content-hashed SimSpace copy
-> selected Codex harness profile
-> Review / Act / Audit
-> safe validation commands + optional NexusSimulator proof
-> recorded evidence and binary diff
-> Review approval
-> separate Apply command with baseline and diff recheck
```

The source workspace is never a Codex working directory. An experiment can
change source files only after validation passes and the host issues both
`experiments.approve` and `experiments.promote`.

## Profiles And Options

Built-in profiles are Conversation, Review-only, Coding, and Audit-only.
Selectable safe options are model, Codex profile, read-only/workspace-write
sandbox, OSS mode, LM Studio/Ollama provider, workspace-contained images,
workspace-contained output schema, and strict config. Dangerous bypass flags,
arbitrary config overrides, danger-full-access, and extra writable roots are not
part of the contract.

Coding profiles require at least one safe validation command or a NexusSimulator
target. Supported validator executables are `npm`, `node`, `dotnet`, and `cargo`;
arguments are passed directly without a shell.

## Record Layout

```text
experiments/<run-id>/
|-- manifest.json
|-- events.jsonl
|-- workspace/
|-- outputs/<attempt>-<stage>.jsonl
|-- outputs/<attempt>-<stage>.stderr.log
|-- outputs/changes.patch
|-- validation-report.json
`-- validation/
    |-- command logs
    |-- nexus-simulator-report.json
    |-- retained screenshots
    |-- retained Playwright trace
    `-- retained WebM session recording
```

## Proof

```bash
npm run check
npm run check:integration
npm run check:harness-live
npm run simulate:contracts
```

The live proof is retained under ignored `output/harness-live-proof/`. It uses
real Codex CLI Review-only and Coding profiles, validates the generated change,
runs NexusSimulator interaction proof, and asserts that source remains unchanged.
The retained simulator evidence includes the full trace/video recording, not only
the final report and screenshots.
