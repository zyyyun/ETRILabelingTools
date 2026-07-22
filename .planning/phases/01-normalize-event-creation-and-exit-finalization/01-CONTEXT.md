# Phase 1: Normalize Event Creation And Exit Finalization - Context

**Gathered:** 2026-07-21
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase defines one canonical Event Waypoint creation flow. It locks when an event becomes a real waypoint, how exit-frame event boxes are interpreted, and how vehicle overlap is treated during event creation. It does not yet solve full event cleanup, post-exit persistence, or deletion UX beyond what is needed to preserve a single creation path.

</domain>

<decisions>
## Implementation Decisions

### Event confirmation timing
- **D-01:** `Entry` is a temporary marker only. An Event Waypoint is created exactly once, only when the user confirms the event through the `Exit` button or `X` shortcut.
- **D-02:** Intermediate event bbox edits before exit may update pending event state, but they must not create waypoint records on their own.

### Exit-frame event bbox semantics
- **D-03:** An event bbox drawn on the exit frame is treated as the final frame box for the current pending event segment.
- **D-04:** An exit-frame event bbox must never create a second Event Waypoint or start a second event candidate.

### Vehicle overlap behavior during creation
- **D-05:** Vehicle overlap does not affect whether an Event Waypoint is created.
- **D-06:** During Phase 1, vehicle overlap may be used only as supporting match context. It must not trigger merge, split, duplicate generation, or extra confirmation logic in the event creation path.

### the agent's Discretion
- The exact internal shape of the pending-event state object or helper methods.
- Whether the single canonical event-finalization path is factored into a dedicated helper or kept inside existing partial-form methods, as long as all entry points route through the same logic.

</decisions>

<specifics>
## Specific Ideas

- The July 20, 2026 fix-request PPT is the source of truth for reproduced failures in this milestone.
- The intended mental model is: `Entry starts a pending segment`, `Exit confirms it once`, and `exit-frame drawing only supplies the last bbox state`.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone scope
- `.planning/PROJECT.md` - Current milestone goal, scope limits, and working assumptions for Event Waypoint stabilization.
- `.planning/REQUIREMENTS.md` - Phase-mapped requirements `EVT-01`, `EVT-02`, and `EVT-03`.
- `.planning/ROADMAP.md` - Phase 1 goal and success criteria.

### Event stability design
- `docs/superpowers/specs/2026-07-21-event-waypoint-stabilization-design.md` - Milestone design rationale and phase boundaries.
- `docs/superpowers/specs/2026-06-29-event-persistence-design.md` - Existing event identity and event-instance stability rules that should not be contradicted by new creation-flow changes.
- `docs/superpowers/plans/2026-06-29-event-exit-textbox-fix.md` - Prior event waypoint list behavior change that may interact with event list assumptions.

### Relevant code
- `WinFormsApp1/Forms/Form1.EntryExit.cs` - Current entry/exit confirmation flow and waypoint creation path.
- `WinFormsApp1/Forms/Form1.EventPropagation.cs` - Event waypoint helper creation and propagation behavior.
- `WinFormsApp1/Forms/Form1.Shortcuts.cs` - Keyboard entry/exit triggers, including `X`.
- `WinFormsApp1/Forms/Form1.Timeline.cs` - Event waypoint list and deletion behavior that consumes created waypoint state.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `SetExitMarkerAndCreateWaypoint` in `WinFormsApp1/Forms/Form1.EntryExit.cs` already acts as the shared exit button flow and is also called from the keyboard shortcut path.
- `WaypointOverlapHelper.FindOverlappingWaypoint(...)` and `WaypointNormalizer.NormalizeInPlace(...)` are already used to control waypoint duplication and range normalization.
- `TrackingIdentityHelper.GetNumericIdentity(...)` is already part of the vehicle path and should remain the only identity source when vehicle context is consulted.

### Established Patterns
- Event boxes already carry `EventInstanceId`, and event groups are built from `EventInstanceId` before waypoint creation in `Form1.EntryExit.cs`.
- Event propagation helpers in `Form1.EventPropagation.cs` currently create or extend boxes in multiple places, which is likely why creation semantics drifted across paths.
- The `X` shortcut currently routes to the same `SetExitMarkerAndCreateWaypoint()` entry, so Phase 1 should preserve that shared entry point rather than invent another exit flow.

### Integration Points
- Pending event lifecycle logic needs to be centered where `entryFrameIndex`, `exitFrameIndex`, `eventBoxesInRange`, and `eventGroups` are resolved in `Form1.EntryExit.cs`.
- Any helper that directly calls `CreateEventWaypoint(...)` or propagates event boxes before finalization in `Form1.EventPropagation.cs` must be reviewed against `D-01` through `D-04`.
- Timeline and deletion code should remain consumers of finalized waypoint state, not co-creators of event segments.

</code_context>

<deferred>
## Deferred Ideas

- Vehicle merge/split stabilization beyond creation-time neutrality belongs to Phase 3.
- Event lifetime clamping, ghost cleanup, and post-exit artifact removal belong to Phase 2.
- Broader selection-state cleanup belongs to Phase 3 unless a Phase 1 fix is required only to preserve the single-creation rule.

</deferred>

---

*Phase: 01-normalize-event-creation-and-exit-finalization*
*Context gathered: 2026-07-21*
