# CentralClient And Shared UI

## Current interaction contract

Primary navigation is Home, Workflows, Apps, and Activity. Settings contains
existing provider/model/device controls and prior coding experiments. Blocks show
one action and one Configure control; configuration and tool implementation details
are outside the canvas. See `17-AUTOMATION-WORKBENCH.md` for current behavior.

New commands: `workflows.list`, `workflows.save`, `workflows.remove`,
`workflows.start`, `workflows.pause`, `workflows.resume`, `workflows.cancel`,
`workflows.approve`, `workflows.clean`, and `apps.configure`.

The following Lab-era layout is retained as migration context; its visual rule is
superseded by the current workbench contract.

## One UI Rule

There is one product UI. Electron and LAN hosting use different transports to
the same CentralClient contract.

```text
apps/web
|-- Lab
|-- Router
|-- Models
|-- Review
`-- Settings
        |
        v
packages/central-client
|-- Electron IPC transport
|-- LAN HTTPS/WebSocket transport
`-- direct test transport
```

## CentralClient Surface

The client exposes outcome-level commands and subscriptions:

```text
getCapabilities()
getWorkspace()
openWorkspace()
createWorkspace()
listProviders()
saveProvider()
testProvider()
listModels()
searchModels()
requestModelLoad()
listPeers()
joinMesh()
updatePeerPolicy()
submitLabRun()
cancelJob()
subscribeJobs()
getReview()
listArtifacts()
getSettings()
```

It does not expose raw IPC channel names, UDP messages, filesystem paths outside
workspace descriptors, private keys, provider secrets, or arbitrary shell
execution.

## UI Ownership

The UI owns:

- visual state and transient form input;
- navigation and responsive layout;
- accessible labels and keyboard behavior;
- optimistic state only when reconciled with service truth;
- presentation of loading, empty, error, and partial states.

The UI does not own:

- durable jobs or queues;
- provider/model/peer truth;
- workspace authorization;
- route selection;
- proof or audit truth;
- execution policy;
- mesh membership.

## Build Path

Use a browser-first app that can produce static assets. Next.js is acceptable
only when the selected features remain exportable or the LAN host intentionally
runs the required server runtime.

Preferred first target:

```text
apps/web build
-> static assets
-> served by Nexus Node LAN host
-> loaded locally by Electron
```

The UI calls `getCapabilities()` at startup and adapts available actions. It
must not branch on hidden assumptions such as "Electron always has filesystem"
or "browser always has ONNX."

## Visual Rule

Preserve the current calm IDE layout: workspace left, conversation right,
minimal top navigation, advanced service controls in their dedicated tabs.
Do not copy static architecture/status text back into the Lab.
