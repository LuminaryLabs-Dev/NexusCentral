# Two-Machine LAN Proof Runbook

## Purpose

Use this runbook to close the final Router seamless-LAN validation step on two real Nexus Central desktops connected to the same LAN.

This runbook assumes the code slices are already landed and the goal is to prove the product contract end to end on physical machines.

## Preconditions

- Both machines run the same Desktop build.
- Both machines can reach each other over the LAN.
- Both machines have LAN control enabled in Router.
- Each machine shows a local LAN token in the right-hand Router workspace.
- At least one machine has two or more loaded runtime instances so imported target multiplicity can be proven.

## Proof Steps

### 1. Discovery

- Launch Desktop on machine A and machine B.
- Open Router on both machines.
- Confirm each machine appears automatically in the other machine’s peer directory.
- Confirm neither machine shows itself as a remote peer row.
- If either machine has multiple network interfaces, confirm only one peer row is shown.

### 2. Initial Trust

- On machine A, select machine B in the peer directory.
- Paste machine B’s token into the selected-peer trust field.
- Trigger pair/repair.
- Confirm machine B moves into a trusted, connected state instead of staying untrusted.
- Confirm imported targets appear for machine B on machine A.

### 3. Remote Inventory

- Confirm one peer can expose multiple imported targets.
- Confirm imported target ids stay stable after manual refresh.
- Confirm target detail shows model, mode, capabilities, and degraded-state messaging.
- Confirm role assignment menus include both local and imported targets.

### 4. Routing

- Assign at least one role on machine A to an imported target owned by machine B.
- Run a request through that role.
- Confirm routing succeeds only when the imported target is trusted and ready.
- Confirm `MaxParallel` blocking appears when oversubscribing the same imported target.

### 5. Auth Failure And Repair

- Rotate the LAN token on machine B.
- Refresh machine B from machine A.
- Confirm machine B moves into `AuthFailed` instead of disappearing or duplicating.
- Confirm cached imported targets remain visible on machine A.
- Re-enter the new token on machine A and repair the peer.
- Confirm the same peer record recovers and imported targets remain attached.

### 6. Stale And Offline

- Disconnect machine B from the network or close the app.
- Confirm machine A first shows a stale/degraded state and later an offline/cached state.
- Confirm imported targets remain visible but unroutable.
- Confirm runtime rejects dispatch to those degraded imported targets with explicit reasoning.

### 7. Restart Continuity

- While machine B is still offline, restart machine A.
- Re-open Router.
- Confirm the trusted offline peer still renders from cached state.
- Confirm cached imported targets still render under the same peer.

### 8. Host Or IP Change

- Bring machine B back online on a changed hostname or IP if available.
- Confirm machine A merges the live discovery back into the same canonical peer.
- Confirm no duplicate trusted peer row appears.
- Confirm imported targets remain owned by the same peer id.

## Success Criteria

The proof is complete only when all of the following are true:

- discovery works automatically,
- trust is durable and repairable,
- imported targets are real and stable,
- degraded state stays visible,
- runtime blocks unsafe imported targets,
- restart continuity holds,
- and no duplicate peer rows appear during repair or address changes.
