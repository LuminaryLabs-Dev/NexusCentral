# Tool Layer Before Workflow Guidance

## Rule Of Thumb

Propose a CLI tool before a workflow when the hard part of the job is mostly:

- structured extraction
- file collection
- metadata normalization
- manifest generation
- contract validation
- repeatable packaging
- deterministic diffing

Use a workflow first when the hard part is mostly:

- prioritization
- interpretation
- sequencing
- tradeoff explanation
- business framing
- follow-up queue design

## In This Workspace

The current workflow library is strongest when it stays narrow and interpretive.

Good workflow roles:

- choose which repo or milestone to inspect
- define why the work matters
- interpret tool output in business terms
- rank gaps and write follow-up recommendations

Good tool roles:

- inventory evidence files
- build manifests
- validate output folders
- snapshot tool contracts
- normalize portfolio records

## Anti-Pattern To Avoid

Do not keep adding workflows that manually re-do:

- file globbing
- manifest assembly
- route extraction
- schema comparison
- output-folder validation

Those are tool-shaped operations. The workflow should consume the result, not emulate the procedure in prose.

## Suggested Layering Pattern

1. Run a CLI tool that emits structured outputs such as JSON, Markdown summaries, and validation status.
2. Run a workflow that reads those outputs and writes the human-facing brief, recommendation queue, or policy note.
3. Keep remediation or implementation as a separate later phase.

