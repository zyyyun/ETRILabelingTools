# Phase 1: Normalize Event Creation And Exit Finalization - Research

**Date:** 2026-07-21
**Status:** Complete

## Research Question

What do we need to know to plan a safe stabilization of Event Waypoint creation so that one user action yields one event segment across button, shortcut, and exit-frame bbox flows?

## Sources Reviewed

- `.planning/PROJECT.md`
- `.planning/REQUIREMENTS.md`
- `.planning/ROADMAP.md`
- `.planning/phases/01-normalize-event-creation-and-exit-finalization/01-CONTEXT.md`
- `docs/superpowers/specs/2026-07-21-event-waypoint-stabilization-design.md`
- `docs/superpowers/specs/2026-06-29-event-persistence-design.md`
- `WinFormsApp1/Forms/Form1.Drawing.cs`
- `WinFormsApp1/Forms/Form1.EntryExit.cs`
- `WinFormsApp1/Forms/Form1.EventPropagation.cs`
- `WinFormsApp1/Forms/Form1.Shortcuts.cs`
- `WinFormsApp1/Forms/Form1.Timeline.cs`
- `WinFormsApp1/Forms/Form1.Json.cs`

## Key Findings

### 1. The exit button and `X` shortcut already share one primary entry point

Evidence:
- `btnExit_Click` calls `SetExitMarkerAndCreateWaypoint()` in `Form1.EntryExit.cs`
- `X` in `Form1.Shortcuts.cs` also calls `SetExitMarkerAndCreateWaypoint()`

Implication:
- We do not need a brand-new external flow for Phase 1.
- The safest change is to make `SetExitMarkerAndCreateWaypoint()` the only place that can finalize an event waypoint.

### 2. Event creation semantics are currently split across multiple helpers

Evidence:
- `CreateEventWaypoint(...)` in `Form1.EventPropagation.cs` can create a waypoint from a single event bbox.
- `PropagateEventBoxIfNeeded(...)` and `PropagateEventBoxToEnd(...)` can extend event boxes based on current waypoint state.
- `SetExitMarkerAndCreateWaypoint()` in `Form1.EntryExit.cs` also groups event boxes and creates event waypoints.

Implication:
- Duplicate waypoint creation is likely caused by these paths drifting apart.
- Phase 1 should explicitly audit and constrain every helper that can create event waypoints or infer event lifetime before exit finalization.

### 3. Event bbox instances already carry a stabilizing identity

Evidence:
- `drawingBox` in `Form1.Drawing.cs` assigns `EventInstanceId = CreateEventInstanceId()` when the current label is `event`.
- `SetExitMarkerAndCreateWaypoint()` groups event boxes by `EventInstanceId` before creating event waypoints.
- `Form1.Json.cs` already prefers `EventInstanceId` for event grouping during load/save.

Implication:
- Phase 1 does not need a new identity model.
- The plan should preserve `EventInstanceId` as the grouping anchor while fixing waypoint creation timing.

### 4. Current entry validation is broader than the desired event mental model

Evidence:
- `SetExitMarkerAndCreateWaypoint()` validates against entry-frame person boxes, vehicle waypoint bodies, entry-frame event boxes, or any event box in the entry-exit range.
- Vehicle overlap is already considered inside the same finalization routine.

Implication:
- The current routine mixes object-presence validation with event-finalization behavior.
- Phase 1 should separate “can finalize this pending event?” from “what surrounding objects exist?” so vehicle overlap cannot branch event creation.

### 5. Timeline and deletion code consume finalized waypoint state, but event lookup still falls back to `EventId`

Evidence:
- `SelectBoxForWaypoint()` in `Form1.Timeline.cs` selects event boxes by `EventId`.
- Event deletion in `btnDeleteSelectedWaypoint_Click()` also deletes by `EventId` + frame range.

Implication:
- Phase 1 can leave full deletion correctness to later phases, but planners should note that consumer code still assumes `EventId` is sufficient.
- Any Phase 1 change to creation timing must avoid worsening this by creating more than one same-`EventId` segment in the same visible range.

## Recommended Planning Direction

### Recommendation A: Make exit finalization the only event waypoint creation authority

Why:
- Matches locked decisions `D-01` through `D-04`
- Reuses the existing shared path for button and shortcut
- Minimizes touch points compared with introducing a new event controller

### Recommendation B: Keep event propagation helpers, but remove their authority to invent waypoint records

Why:
- Propagation helpers are still useful for box extension once a range is known.
- Their authority should be reduced from “create and extend event semantics” to “copy boxes within an already finalized range.”

### Recommendation C: Preserve vehicle context as read-only for Phase 1

Why:
- The roadmap explicitly defers vehicle identity stabilization to Phase 3.
- This avoids expanding Phase 1 into overlap-merge logic while still allowing future matching hooks.

## Plan-Shaping Constraints

- `Form1.EntryExit.cs` and `Form1.EventPropagation.cs` are both in scope and likely both need changes.
- Because these files are tightly coupled around event finalization, the phase is better planned as sequential work rather than parallel edits.
- Existing working-tree modifications already exist in other files, so plans should target only the files necessary for this phase and avoid incidental cleanup.

## Risks

- A hidden call site may still invoke `CreateEventWaypoint(...)` or `PropagateEventBoxIfNeeded(...)` outside the canonical finalization path.
- Exit-frame bbox handling may still be duplicated if draw-time logic mutates event boxes in-place before finalization.
- Consumers that still resolve event state by `EventId` may mask or reintroduce duplication if finalization does not aggressively dedupe by `EventInstanceId`.

## Planning Recommendations

1. First plan should identify and unify all event finalization entry points.
2. Second plan should strip eager waypoint creation from helper paths and add regression checks for `Exit` button, `X`, and exit-frame drawing.
3. Both plans should verify behavior with at least a solution build and a manual replay checklist tied to `EVT-01` through `EVT-03`.

## Source Audit

| Source | ID | Requirement Or Decision | Coverage Direction |
|--------|----|--------------------------|--------------------|
| GOAL | Phase 1 | One canonical event creation flow | Needs both plans |
| REQ | EVT-01 | One event segment per entry/exit action | Plan 01 + 02 |
| REQ | EVT-02 | Exit button and `X` share lifecycle logic | Plan 01 |
| REQ | EVT-03 | Exit-frame bbox does not create second waypoint | Plan 02 |
| CONTEXT | D-01 | `Entry` temporary, `Exit/X` finalizes | Plan 01 |
| CONTEXT | D-03 | Exit-frame bbox is final box only | Plan 02 |
| CONTEXT | D-05 | Vehicle overlap does not drive creation | Plan 01 + 02 |

## Conclusion

Research supports a two-plan, sequential stabilization approach:
- first centralize authority for final event creation
- then remove eager side effects and verify the reproduced behaviors

This is enough to plan safely without external library research.
