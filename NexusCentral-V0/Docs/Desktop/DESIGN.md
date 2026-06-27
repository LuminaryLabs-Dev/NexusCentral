# Desktop UI DESIGN

## Purpose

This document is the source of truth for Desktop UI structure and styling consistency in NexusCentral.

It exists to:

- define one desktop-tool grammar for the Avalonia app
- keep future UI work structurally consistent without restructuring the product
- describe the current visual DNA that should remain stable
- define the layout contract that all tabs and shell utilities must follow
- identify existing drift and the migration path back to the contract

## One-Line Contract

The Desktop app is a fixed shell of split workspaces, where each pane is composed of contextual, collapsible header/body sections with icon-only header actions.

## Scope

This document governs the Avalonia Desktop surface in `Desktop/`.

It applies to:

- global shell structure
- tab workspace structure
- pane organization
- section anatomy
- shared colors and semantic styles
- typography, spacing, radius, and border usage
- action hierarchy and disclosure behavior
- tab-level structural decisions

It does not currently govern the Web frontend because the frontend source is not fully present in this repository snapshot.

## Non-Goals

- redesign the product visual direction
- replace Avalonia `FluentTheme`
- restructure services or MVVM boundaries
- create a second parallel styling system
- allow each screen to invent its own layout grammar
- treat visual cleanup as separate from structural consistency

## Source Of Truth

The canonical Desktop foundation already exists in code:

- `Desktop/App.axaml`
- `Desktop/Styles/Colors.axaml`
- `Desktop/Styles/Styles.axaml`

The implementation rule is:

1. Prefer shared semantic styles from `Styles.axaml`.
2. Prefer shared tokens from `Colors.axaml` before local values.
3. Promote repeated structures into shared primitives before adding more view-local layout rules.
4. Keep structure owned by the design system and content owned by the screen.

## Visual DNA

### Product Tone

The Desktop UI is a dark operator console.

Its visual character is:

- dense but readable
- operational rather than decorative
- structured through borders and contrast instead of shadows
- pane-based instead of canvas-based
- compact enough for high-information tooling

### Theme Foundation

Base theme:

- Avalonia `FluentTheme`

App identity layer:

- Tokyo-style dark palette
- semantic pane and control classes
- border-driven structure
- compact operator controls

### Color DNA

Canonical token families in `Desktop/Styles/Colors.axaml`:

- background: `TokyoBG`, `TokyoShell`
- surfaces: `TokyoSurface`, `TokyoPanel`, `TokyoPanelAlt`, `TokyoInput`
- structure: `TokyoSecondary`, `TokyoDivider`, `TokyoDividerStrong`
- emphasis: `TokyoAccent`, `TokyoAccentHover`
- text: `TokyoFG`, `TokyoFGBright`, `TokyoFGDim`, `TokyoFGMuted`
- status: `TokyoSuccess`, `TokyoWarning`, `TokyoError`

Color usage intent:

- `TokyoBG` is the app backdrop
- `TokyoShell` is for shell surfaces and fixed bars
- `TokyoSurface` and `TokyoPanel` are for workspace and section bodies
- `TokyoInput` is for editable or code-like regions
- `TokyoDivider` and `TokyoDividerStrong` are the main structural separators
- `TokyoAccent` is reserved for active focus, meaningful emphasis, and primary-positive state
- `TokyoError` is reserved for destructive or fault states

### Typography DNA

Default typography:

- UI text: `Inter, Segoe UI, Arial`
- code and ids: `Cascadia Code, Consolas, monospace`

Typography roles:

- `pageTitle`
- `pageMeta`
- `sectionTitle`
- `sectionMeta`
- `fieldLabel`
- `inlineLabel`
- `monoText`

Typography rules:

- prefer semantic text classes over per-view `FontSize` and `FontWeight`
- section headers should read as structural labels, not decorative headlines
- use `pageMeta` and `sectionMeta` for support text instead of local text variants

### Spacing DNA

The product uses a compact systems-tool spacing model.

Observed spacing families:

- `3-4`: chip and inline spacing
- `6-8`: compact control group spacing
- `10-12`: standard section padding and row spacing
- `14-16`: major workspace and rich-body spacing

Spacing rules:

- inline controls cluster at the `3-8` scale
- section headers and compact bodies cluster at the `8-12` scale
- richer editing and output bodies cluster at the `12-16` scale
- avoid arbitrary spacing values when an existing family works

### Radius DNA

Canonical radius roles:

- `0`: shell edges, nav strips, hard structural boundaries
- `3`: default small control radius
- `6`: compact affordance radius
- `8-12`: rich body or editor surface radius
- `999`: pill chip or badge radius

Rules:

- shell structure should stay square or nearly square
- compact controls should stay small-radius
- richer bodies may use medium radii when they are clearly owned by one section
- pill radii are reserved for status chips and compact labels

### Border DNA

The Desktop UI is structured more by borders than by elevation.

Border rules:

- use `TokyoDividerBrush` for standard boundaries
- use `TokyoDividerStrongBrush` for stronger emphasis
- accent borders are for active, selected, or target states only
- prefer one clear ownership border over decorative nested border stacks

### Layering DNA

The Desktop UI is embedded and pane-based.

Canonical layering order:

1. app shell
2. split workspace
3. pane stack
4. section header
5. section body
6. subsection or owned inline region
7. badge, chip, state accent, or disclosure detail

Not part of the product DNA:

- floating dashboard cards
- modal-heavy interaction
- shadow-driven depth systems
- popover-driven workflows
- standalone Workflow top-level tab

## Structural Contract

### Global Shell

The global shell is fixed.

Canonical shell structure:

- top navigation strip
- single active workspace host
- bottom collapsible utility dock

This shell exists in:

- `Desktop/Views/MainWindow.axaml`
- `Desktop/Components/CollapsibleDock.axaml`

The shell is not the place for per-tab invention.

### Tab Workspace Contract

Every full feature tab must be a split workspace.

Required structure:

- left pane
- right pane

Tabs do not decide whether they are split.
Tabs decide what each side contains.

The split workspace is the default page grammar for:

- Lab
- Automations
- Models
- Router
- Agents
- Integrations

### Pane Contract

Each pane is a vertical stack of contextual sections.

Each pane is responsible for structure, not decoration.

Pane rules:

- panes are stacks, not freeform canvases
- panes should have clear ownership boundaries
- panes should not mix unrelated contexts in one region
- panes should prefer scanability over novelty

### Left Pane Contract

The left pane is for:

- navigation
- search and filters
- inventory and lists
- compact summaries

Left-pane rules:

- compact and highly scannable
- lower density of deep editing controls
- frequent collapse is expected
- headers still use icon-only actions

### Right Pane Contract

The right pane is for:

- selected item overview
- configuration
- diagnostics
- outputs
- logs
- richer editing and control surfaces

Right-pane rules:

- bodies may be richer and deeper here
- detail is allowed, but still inside the same section contract
- right panes should not become unowned stacks of random cards

### Section Contract

Every major region inside a pane is a section.

A section always has:

- `SectionHeader`
- `SectionBody`

The header is the identity of the section.
The body is the working surface of the section.

Each section must define:

- title
- optional subtitle or status
- icon action cluster
- collapsible body

### Header Contract

Every section header must contain:

- left: title and contextual metadata
- right: icon actions only

Allowed header actions:

- expand or collapse
- add
- remove
- refresh
- pin
- open
- more
- run or play
- stop
- edit
- duplicate

Header rules:

- no text buttons in section headers
- no mixed icon and text actions in section headers
- use tooltips for icon meaning
- use a standard icon set
- action order should be consistent across the app
- header height, padding, alignment, and icon sizing should be consistent

### Body Contract

The body is where editing, forms, logs, outputs, and detailed controls live.

Body rules:

- bodies may contain explicit text buttons where clarity matters
- bodies own editing surfaces and dense controls
- body padding and inset must visually read as owned by the header
- bodies should not restyle themselves into a second structural system

### Collapse Contract

Bodies may collapse when density is high.

Collapse rules:

- the header always remains visible
- collapsing should reduce noise, not hide critical state
- the collapsed header must preserve enough context to stay informative

Collapsed headers should still communicate:

- section name
- important status
- short summary of the hidden body

Example summary pattern:

- `Loaded Instances`
- `5 active · 2 thinking · 1 warning`

### Grouping Contract

Sections must be grouped by user intent and domain context.

Good grouping domains:

- runtime
- configuration
- resources
- workflow
- outputs
- health
- routing
- memory
- tools
- attachments

Bad grouping patterns:

- giant generic `settings`
- random `advanced`
- controls mixed with logs and metadata with no shared purpose

Each section should answer at least one of these questions:

- what is this thing
- what state is it in
- what can I do with it
- what details matter next

### Action Hierarchy

The app uses three action levels:

- section header actions: icon-only
- section body actions: explicit controls, including text buttons when clarity matters
- page-level actions: rare global commands for the workspace

Rules:

- do not put body-level text actions into headers
- do not turn every section header into a mini toolbar
- page-level commands should remain rare and clearly global

### Nesting Contract

Visible nesting depth should stay shallow.

Allowed visible depth:

- pane section
- optional subsection

Not allowed as a normal pattern:

- section
- card
- subsection
- subcard
- inner box
- inner toolbar

The default maximum is two visible levels.

### Shell Utilities

`Monitor` and the dock are shell utilities, not full feature workspaces.

They still borrow the same section contract:

- visible headers
- icon-style compact actions in shell surfaces
- bodies that own detail
- contextual grouping instead of arbitrary clustering

They are exceptions to the split-workspace rule because they are shell-level utilities, not primary tabs.

## Canonical Shared Vocabulary

### Existing Shared Roles

Text roles already present in `Desktop/Styles/Styles.axaml`:

- `pageTitle`
- `pageMeta`
- `sectionTitle`
- `sectionMeta`
- `fieldLabel`
- `inlineLabel`
- `monoText`

Container roles already present:

- `shellStrip`
- `pageHost`
- `pagePane`
- `pageScaffold`
- `pageHeader`
- `toolbarStrip`
- `composerBar`
- `paneSection`
- `workflowCard`
- `workflowDetails`
- `inlineSection`
- `inlineBadge`
- `inlineField`
- `bannerCluster`
- `inspectorGroup`
- `transcriptRow`
- `workspaceSection`

Control roles already present:

- `navTab`
- `primary`
- `secondary`
- `destructiveSecondary`
- `cardAction`
- `workflowToggle`
- `iconSecondary`
- `compactAction`
- `flat`
- `compactCombo`

State roles already present:

- `elementSelected`
- `elementActive`
- `toolTarget`

### Target Structural Vocabulary

The contract should be expressed in shared primitives that map onto the existing style layer.

Canonical structural primitives:

- `SplitWorkspaceView`
- `PaneStack`
- `PaneSection`
- `CollapsibleSection`
- `SectionHeader`
- `IconActionBar`
- `SectionBody`

Implementation rule:

- reuse existing shared styles where they already fit
- rename or promote repeated patterns when the current names are too narrow
- do not leave the new contract as documentation-only vocabulary forever

## Tab Contract Mapping

### Lab

Archetype:

- split workspace execution surface

Left pane:

- workspace navigation
- context summaries
- compact project and export controls

Right pane:

- transcript
- run details
- reasoning
- composer

Contract fit:

- strong fit for split workspace
- already close to section-based thinking
- needs section naming and collapse consistency more than visual redesign

### Automations

Archetype:

- split workspace workflow ladder

Left pane:

- workflow inventory
- compact filters or summaries

Right pane:

- selected workflow overview
- stacked workflow sections
- step editing bodies
- outputs and execution controls

Contract fit:

- workflow-only surface
- strong fit for contextual section stacks
- should remain the sole workflow authoring tab

### Models

Archetype:

- split workspace inventory and inspector

Left pane:

- model catalog
- intake
- filtering

Right pane:

- selected model summary
- configuration
- runtime details
- utilities

Contract fit:

- one of the best existing references for contract adoption
- needs stricter header/body consistency and collapse summaries

### Router

Archetype:

- split workspace control plane

Left pane:

- peer directory
- queue or routing summaries
- filters

Right pane:

- selected peer overview
- machine configuration
- imported targets
- route roles

Contract fit:

- strong fit for contextual grouping
- should become a reference implementation for control and diagnostics sections

### Agents

Archetype:

- split workspace admin inspector

Left pane:

- wrapper inventory
- run lists
- compact summaries

Right pane:

- selected agent overview
- configuration
- diagnostics
- outputs

Contract fit:

- structurally close
- currently drifts through local card rules, heading styles, and section identity

### Integrations

Archetype:

- split workspace integrations console

Left pane:

- integration inventory
- state summaries
- enablement controls

Right pane:

- selected integration overview
- tool configuration
- routing or endpoint details
- logs

Contract fit:

- structurally viable
- currently drifts through local uppercase headings, local radii, and inconsistent section surfaces

### Monitor

Archetype:

- shell utility health surface

Role:

- compact status and quick control region

Contract fit:

- should borrow section rules in miniature form
- not a full split workspace

### Dock

Archetype:

- shell utility container

Role:

- persistent utility host for secondary operational surfaces

Contract fit:

- uses shell utility rules, not full workspace rules

## Deprecated And Drift Patterns

The following patterns are considered drift against the contract:

- freeform page layouts that are not split workspaces
- panes made of unrelated cards instead of contextual section stacks
- text buttons inside section headers
- mixed icon and text actions inside section headers
- local heading systems that bypass shared text roles
- arbitrary `advanced` groupings with no contextual identity
- bodies that collapse without preserving useful summary state
- deep visible nesting beyond two levels
- hardcoded colors and direct `White` usage inside views
- local radius and spacing systems that bypass shared roles
- per-view admin card systems that duplicate shared structure

Known high-drift examples in the current codebase:

- `Desktop/Views/AgentsView.axaml`
- `Desktop/Views/McpView.axaml`

## Exceptions Policy

An exception is allowed only when all of the following are true:

- the current contract cannot express the UI clearly
- the pattern is meaningfully different, not just locally preferred
- the new pattern can be named and reused
- the exception is documented here before or alongside implementation

If a structural exception appears in more than one screen, it stops being an exception and becomes a shared primitive candidate.

## Migration Plan

### Phase 1: Lock The Contract

Use this document as the authoritative rule set for Desktop UI structure.

Immediate outcome:

- stop adding new layouts that do not fit the split workspace and section contract

### Phase 2: Align Shared Primitives

Update shared styles and primitives in `Desktop/Styles/Styles.axaml` so the contract has reusable implementation parts.

Focus:

- section header structure
- icon action sizing and spacing
- section body padding and ownership rules
- consistent collapse affordances

### Phase 3: Convert Reference Tabs

Normalize the strongest candidate tabs first:

- Models
- Router
- Lab

Goal:

- make them reference implementations of the contract

### Phase 4: Convert High-Drift Tabs

Normalize the tabs with the most local structural dialect:

- Agents
- Integrations
- Automations

Goal:

- remove local section systems
- replace ad hoc cards with contextual section stacks

### Phase 5: Remove Deprecated Patterns

Sweep remaining views for:

- text buttons in headers
- mixed header action styles
- arbitrary grouping
- deep visible nesting
- hardcoded structural values

### Phase 6: Enforce In Review

Require new UI work to pass the contract checklist before merge.

## Review Checklist

Before merging a UI change, verify:

- is this a split workspace if it is a full feature tab
- does each pane read as a contextual stack of sections
- does every major section have a visible header and owned body
- are section header actions icon-only
- are body actions kept inside the body where clarity matters
- does collapsed state preserve meaningful summary information
- is the grouping contextual instead of arbitrary
- does the left pane stay compact and scannable
- does the right pane own richer detail and editing
- is visible nesting within the two-level limit
- are shared text, color, spacing, and border roles used before local values
- if a new pattern was required, was this document updated

## Ownership

This document should be updated whenever:

- a shared structural primitive is added or renamed
- a repeated local pattern is promoted into shared vocabulary
- a tab materially changes how it expresses the split workspace contract
- a major cleanup changes the canonical desktop grammar

The intent is not to freeze the UI.
The intent is to make the app evolve in one direction instead of several.
