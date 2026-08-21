# Per-Feature Report

| Feature | Ensure | Validation | Proof | Result | Residual risk |
|---|---|---|---|---|---|
| Matrix authority | Existing matrix, ledger, and queue preserved | Schema and revision checks | Revision 3, 313 nodes | Pass | Future writes must reject stale revisions. |
| Goal hierarchy | Approved target architecture | Parent, field, owner, gate, and leaf checks | 16 domains, 68 capabilities, 112 subgoals, 116 atomic actions | Pass | Product nodes remain unexecuted. |
| DAG and routing | All dependencies reference matrix nodes | Orphan and cycle traversal | No missing reference or cycle | Pass | Runtime route behavior remains unproven. |
| Profile and API boundaries | One shared application contract | Semantic audit and failure simulation | Desktop, public web, internal web, and internal API states represented | Pass | Server enforcement must be implemented and tested later. |
| Harness catalog | Local inspection and official interface references | Admission and authority review | 11 harnesses, 4 model classes | Pass | Conditional and held adapters need live qualification. |
| Domain simulations | Fixed expected outcome and invariants | First pass plus review pass | 16 of 16 scenarios | Pass | Forecasts are not runtime evidence. |
| Harness simulations | Cataloged harness/model combinations | Suitability, tools, route, and sandbox review | 12 scenarios | Pass | Three provider-native/full-agent routes remain speculative and held. |
| Ledger and queue | Append-only history and derived projection | Last-transition and ready-leaf reconciliation | 26 events, 0 active, 1 ready | Pass | Rebuild queue after every transition. |
| Repository regression | Existing workspace dependencies | `npm run check` | Core checks, workspace syntax checks, and static Next.js build passed | Pass | This is not live feature proof. |
| Human-view requirement | No visible surface changed | Relevance check | Browser proof not applicable to this run | Pass | UI and release nodes still require later human-view proof. |

## Overall

The full goal matrix is set up and realistic as a planning control plane. Only matrix-control setup is promoted. Application, branch, harness, model, deployment, and release work remains planned and unproven. The next unclaimed leaf is `source.inventory.snapshot.worktree`.
