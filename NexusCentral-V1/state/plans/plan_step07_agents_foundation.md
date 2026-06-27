# Step 07: Agents Foundation

## One Sentence

Add the Agents foundation for harness manifests, tool permissions, and registry-backed agent rows.

## State Observes

- Agents UI is currently hard-coded planned rows.
- Tool contracts are not discovered from manifests, RPC, or permission policy.
- Add harness and template controls are disabled.
- V2 wrapper behavior is useful as reference but V2 manifest shape should not be ported directly.

## Exact Changes To Plan

- Add normalized harness manifest model:
  - id
  - name
  - description
  - supported task types
  - required tools
  - allowed file scopes
  - model requirements
  - execution mode
- Add harness registry service.
- Add tool permission policy:
  - workspace read
  - workspace write
  - network
  - shell
  - provider calls
- Bind Agents UI to registry.
- Show empty state if no harnesses exist.
- Add manifest validation before allowing harness activation.

## Hero Controls / User-Facing Impact

- View harnesses.
- Inspect harness permissions.
- Validate manifest.

## Advanced Controls / Deferred

- Real harness execution.
- Tool launcher.
- Template catalog execution.
- Wrapper run history.

## Validation

- Empty registry shows truthful empty state.
- Sample normalized manifest validates or fails clearly.
- Invalid permissions block activation.
- Build and tests pass.

## Done Criteria

- Agents tab reflects real registry state.
- Harness rows are not hard-coded UI claims.
- Permission policy exists before execution behavior.
