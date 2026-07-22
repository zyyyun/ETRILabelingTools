# Phase 3: Stabilize Vehicle Coupling And Selection Safety - Research

**Date:** 2026-07-21
**Status:** Complete

## Research Question

What do we need to know to stop event workflows from splitting or duplicating vehicles, and how should list selection ownership be enforced so delete actions always target the intended waypoint list?

## Sources Reviewed

- `.planning/PROJECT.md`
- `.planning/REQUIREMENTS.md`
- `.planning/ROADMAP.md`
- `.planning/STATE.md`
- `.planning/phases/03-stabilize-vehicle-coupling-and-selection-safety/03-CONTEXT.md`
- `.planning/phases/02-clamp-event-lifetime-and-cleanup-derived-artifacts/02-CONTEXT.md`
- `.planning/phases/02-clamp-event-lifetime-and-cleanup-derived-artifacts/02-01-SUMMARY.md`
- `.planning/phases/02-clamp-event-lifetime-and-cleanup-derived-artifacts/02-02-SUMMARY.md`
- `docs/superpowers/specs/2026-07-21-event-waypoint-stabilization-design.md`
- `WinFormsApp1/Logic/TrackingIdentityHelper.cs`
- `WinFormsApp1/Forms/Form1.EntryExit.cs`
- `WinFormsApp1/Forms/Form1.Timeline.cs`
- `WinFormsApp1/Forms/Form1.Drawing.cs`
- `WinFormsApp1.Tests/Program.cs`

## Key Findings

### 1. `TrackingIdentityHelper` is already the right vehicle identity backbone

Evidence:
- `GetNumericIdentity(...)` uses `VehicleInstanceId` as the primary identity for vehicle bodies.
- Plates already resolve through `LinkedVehicleInstanceId`, which means related vehicle sub-boxes already follow instance-based identity.
- `MatchesWaypoint(...)` for vehicles already compares against `GetNumericIdentity(...)`, giving us one clear identity path to build on.

Implication:
- Phase 3 should not introduce another identity scheme.
- It should push more call sites to use `TrackingIdentityHelper` consistently instead of falling back to `VehicleId`.

### 2. Event-assisted flows still touch vehicle waypoint creation and merge logic

Evidence:
- In `Form1.EntryExit.cs`, the same exit finalization routine that handles event creation also iterates over `entryVehicleBoxes` and can merge or create vehicle waypoints in the same pass.
- This means event workflows still share a code path that can mutate vehicle state even though the user wants `event` to remain independent from `vehicle`.

Implication:
- Phase 3 should isolate event-side behavior from vehicle-side creation side effects.
- The most direct fix is to prevent event-driven work from causing new vehicle boxes or implicit new vehicle identities.

### 3. Stale list selection is still structurally possible

Evidence:
- `btnDeleteSelectedWaypoint_Click()` in `Form1.Timeline.cs` checks selected items in a fixed order: person -> vehicle -> event.
- That ordering means an earlier person selection can still win even when the user is visually acting in another list.
- Individual list click handlers update `selectedWaypoint`, but they do not explicitly clear other list selections as a global ownership rule.

Implication:
- Delete safety cannot rely only on a single selectedWaypoint variable.
- Phase 3 should establish “active list ownership” explicitly and clear or ignore stale selections from other lists.

### 4. Timeline-side box selection also still has older event/vehicle assumptions

Evidence:
- `SelectBoxForWaypoint(...)` already uses `TrackingIdentityHelper.MatchesWaypoint(...)` for vehicles, which is good.
- Event selection still resolves by `EventId`, which was acceptable for earlier phases but reinforces the need for vehicle stability to be solved independently here.
- `listViewWaypoints_MouseDown(...)` routes frame navigation and box selection based on the list view that generated the hit event, so list ownership logic is already partly localized there.

Implication:
- Phase 3 can likely enforce ownership in list click/mousedown handlers plus delete routing without redesigning the whole timeline.

### 5. Existing tests already cover part of the vehicle identity story

Evidence:
- `WinFormsApp1.Tests/Program.cs` already contains tests for:
  - same-type vehicles receiving different instance ids
  - `TrackingIdentityHelper.MatchesWaypoint(...)` for bodies and linked plates
  - vehicle waypoint overlap normalization

Implication:
- Phase 3 should extend the existing lightweight test harness rather than inventing a new one.
- Missing regression coverage is specifically around event-assisted vehicle duplication and list ownership safety.

## Recommended Planning Direction

### Recommendation A: Treat `VehicleInstanceId` as the only internal vehicle identity for active logic

Why:
- It already exists and is exercised by helpers and tests.
- Using anything else in active logic is what lets one logical vehicle split into multiple visible identities.

### Recommendation B: Block event workflows from creating new vehicle boxes

Why:
- The user explicitly wants event and vehicle behavior to remain independent.
- Preventing new vehicle bbox generation at the event-assisted step is cleaner than allowing duplicates and deduping later.

### Recommendation C: Enforce active list ownership before delete/edit

Why:
- The stale-selection issue is a routing problem, not just a UI polish issue.
- Current list selection should become authoritative and other list selections should be cleared or ignored.

## Risks

- Some legacy flows may still assume `VehicleId` is the visible or persistent identifier in UI text.
- If event-assisted workflows currently rely on implicit vehicle creation as a fallback, blocking that path could reveal missing manual steps that were previously hidden.
- Selection ownership changes can feel abrupt if list clearing happens too aggressively.

## Planning Recommendations

1. First plan should finish the identity pass: entry/exit paths and helper usage must resolve vehicles only through `VehicleInstanceId`.
2. Second plan should stop event-assisted vehicle duplication and add regression tests for overlap cases.
3. Third plan should enforce list ownership and add a delete-safety regression hook so person stale selection cannot win over current vehicle/event intent.

## Source Audit

| Source | ID | Requirement Or Decision | Coverage Direction |
|--------|----|--------------------------|--------------------|
| GOAL | Phase 3 | Stabilize vehicle coupling and selection safety | Needs all plans |
| REQ | TRK-01 | No duplicate vehicle boxes during event overlap | Plan 02 |
| REQ | TRK-02 | Same logical vehicle keeps one identity | Plan 01 + 02 |
| REQ | UI-01 | Current list reflects active target | Plan 03 |
| REQ | UI-02 | Empty click clears active selection | Plan 03 |
| REQ | UI-03 | Wrong waypoint is not deleted from stale selection | Plan 03 |
| CONTEXT | D-02 | `VehicleInstanceId` is the single source of truth | Plan 01 |
| CONTEXT | D-04 | Event work must not auto-create a new vehicle bbox | Plan 02 |
| CONTEXT | D-07 | Current list owns selection | Plan 03 |

## Conclusion

Research supports a three-plan Phase 3:
- normalize vehicle identity usage
- stop event-assisted vehicle duplication at the source
- enforce list ownership so delete and edit actions target the user’s actual current list

This can be done incrementally on top of the Phase 1 and Phase 2 event fixes without reopening event lifetime rules.
