# Tool Validation Note: `<tool_id>`

Date: `YYYY-MM-DD`
Status: `prepared for experimental review` or `prepared for final placement`

## Placement Decision

- `proposal_source`: `proposals/tool_proposals/<tool_id>.md`
- `tool_spec`: `tools/experimental_tools/<tool_id>.yaml` or `tools/final_tools/<tool_id>.yaml`
- `template_asset`: `tools/tool_templates/<tool_specific_template>.yaml`
- `current_decision`: Explain why this tool belongs in `experimental_tools/` or `final_tools/` right now.

## Contract Checks To Run First

- Confirm the command surface stays limited to the documented subcommands.
- Confirm the primary handoff files are sufficient for downstream workflows without ad hoc extras.
- Confirm source inputs remain read-only and outputs stay scoped to the selected output directory.
- Confirm each finding cites the exact path, identifier, or rule that triggered it.

## Minimum Fixture Coverage

- Name one realistic fixture family the tool must survive first.
- Name the second fixture family or drift scenario required before promotion.

## Workflow Handshake To Verify

- State which workflow should call the tool first.
- Name the specific output files those workflows should consume.
- Keep interpretation, prioritization, and business framing assigned to the workflow rather than the tool.

## Promotion Gate

- Describe the narrow stability proof that justifies promotion or wider reuse.

## Current Risks

- Risk one that could force schema churn or command expansion.
- Risk two that could blur the tool versus workflow boundary.
