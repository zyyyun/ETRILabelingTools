# Phase 2: Clamp Event Lifetime And Cleanup Derived Artifacts - Research

**Date:** 2026-07-21
**Status:** Complete

## Research Question

What do we need to know to make event boxes disappear exactly at `ExitFrame`, clean up only the targeted event instance, and align event handling with the manual-tracking-first behavior referenced from `AOLTv1.0`?

## Sources Reviewed

- `.planning/PROJECT.md`
- `.planning/REQUIREMENTS.md`
- `.planning/ROADMAP.md`
- `.planning/STATE.md`
- `.planning/phases/02-clamp-event-lifetime-and-cleanup-derived-artifacts/02-CONTEXT.md`
- `.planning/phases/01-normalize-event-creation-and-exit-finalization/01-CONTEXT.md`
- `.planning/phases/01-normalize-event-creation-and-exit-finalization/01-01-SUMMARY.md`
- `.planning/phases/01-normalize-event-creation-and-exit-finalization/01-02-SUMMARY.md`
- `docs/superpowers/specs/2026-07-21-event-waypoint-stabilization-design.md`
- `docs/superpowers/specs/2026-06-29-event-persistence-design.md`
- `WinFormsApp1/Forms/Form1.EventPropagation.cs`
- `WinFormsApp1/Forms/Form1.EntryExit.cs`
- `WinFormsApp1/Forms/Form1.Json.cs`
- `WinFormsApp1/Forms/Form1.Inertia.cs`
- `WinFormsApp1/Forms/Form1.Timeline.cs`
- `C:/Users/ANNA/AOLTv1.0/Forms/MainForm.cs`

## Key Findings

### 1. Current cleanup is still broad and shape-based in important event paths

Evidence:
- `TerminateEventFromCurrentFrame()` in `Form1.Json.cs` deletes event boxes by `EventId` plus rectangle match from the current frame onward.
- `RemovePropagatedEventBoxes()` in `Form1.EventPropagation.cs` also deletes by `EventId` plus rectangle match and can remove the waypoint by entry frame.
- `btnDeleteSelectedWaypoint_Click()` in `Form1.Timeline.cs` deletes event boxes by `EventId` and frame range.

Implication:
- Phase 2 cannot be a tiny one-line cap change.
- Cleanup logic must move from `EventId`/rectangle heuristics to `EventInstanceId`-scoped deletion to avoid harming sibling events of the same type.

### 2. Current propagation helpers still allow event extension after finalization

Evidence:
- `PropagateEventBoxWithinRange(...)` fills missing event boxes through a supplied end frame.
- `PropagateEventBoxIfNeeded(...)` can still create event boxes forward from the current frame to waypoint exit.
- `PropagateEventBoxFromCurrentFrame(...)` mutates or creates later event boxes across the remaining waypoint range.

Implication:
- Phase 2 must either disable or sharply narrow these helpers for event segments.
- The manual-tracking-first rule from the PPT and AOL reference is not compatible with keeping these as normal post-finalization behaviors.

### 3. `AOLTv1.0` uses immediate exit shortening and bounded refill

Evidence:
- In `AOLTv1.0/Forms/MainForm.cs`, `SetExitMarkerAndCreateWaypoint()` shortens an existing waypoint exit by deleting boxes beyond the new exit immediately.
- The same flow then reuses `PropagateEventBoxWithinRange(...)` only to refill the valid bounded range.
- `TerminateEventFromCurrentFrame()` removes future event boxes and updates the waypoint exit to `currentFrame - 1`.

Implication:
- The AOL reference favors immediate in-memory trimming, not save-time cleanup.
- It also treats propagation as a bounded range tool, not a free-running auto extension system.

### 4. The local codebase already has the building blocks for instance-safe cleanup

Evidence:
- Phase 1 added `EventFinalizationHelper.NormalizePendingEventInstanceIds(...)` before event grouping.
- `FindWaypointForBox(...)` already prefers `EventInstanceId` when resolving an event waypoint for a box.
- `BoundingBox` and `WaypointMarker` already carry `EventInstanceId`.

Implication:
- Phase 2 does not need a new identity system.
- It should build a cleanup helper around the existing instance id rather than inventing another key.

### 5. Inertia currently has its own deletion model that must be aligned

Evidence:
- `Form1.Inertia.cs` contains disappearance handling and `DeleteBoxesInRange(...)`, which marks boxes deleted across waypoint ranges.
- The deletion helper is keyed by `label + ObjectId`, which for events still implies broader object-level handling than instance-level handling.

Implication:
- Event-specific range clipping cannot stop at `Json` and `Timeline`.
- Inertia or disappearance cleanup needs a Phase 2 pass so hidden stale event boxes do not survive through another maintenance path.

## Recommended Planning Direction

### Recommendation A: Add one event-instance cleanup/clamp helper and route all event shortening paths through it

Why:
- Reduces the chance of fixing `Q` termination while leaving delete or propagation behavior inconsistent.
- Matches the Phase 2 decision that cleanup is instance-scoped and exit-bound.

### Recommendation B: Treat event propagation as bounded range-fill only

Why:
- Aligns with both the user’s AOL reference and the milestone decision that event handling becomes manual-tracking-first.
- Preserves the narrow useful part of propagation without keeping auto-extension as a normal behavior.

### Recommendation C: Use save-time trimming as a safety net, not the primary cleanup mechanism

Why:
- The user explicitly wants overflow gone in UI and memory, not only after save.
- Save-time trimming is still worth keeping as a final consistency guard.

## Risks

- Some event editing flows may currently rely on `PropagateEventBoxFromCurrentFrame(...)` after a manual resize or drag.
- Deleting by `EventInstanceId` may reveal older data that never got an instance id assigned cleanly.
- Inertia/disappearance code may continue to regenerate or preserve stale event boxes unless it is aligned with the new hard cap.

## Planning Recommendations

1. First plan should centralize instance-scoped event trimming for `Q` termination, exit shortening, and save-time clamp.
2. Second plan should disable or narrow post-finalization event propagation/retracking and update delete behavior in timeline/inertia paths.
3. Verification should include a replay checklist for:
   - `Q` termination from a mid-range frame
   - exit shortening on an existing event waypoint
   - deleting one of two same-type events without touching the other
   - ensuring no event boxes survive after `ExitFrame`

## Source Audit

| Source | ID | Requirement Or Decision | Coverage Direction |
|--------|----|--------------------------|--------------------|
| GOAL | Phase 2 | Clamp event lifetime and remove orphaned artifacts | Needs both plans |
| REQ | EVT-04 | Event disappears after exit | Plan 01 |
| REQ | EVT-05 | Event deletion removes linked artifacts | Plan 01 + 02 |
| REQ | TRK-03 | No event overflow from propagation/retracking | Plan 02 |
| CONTEXT | D-03 | Cleanup by `EventInstanceId` | Plan 01 + 02 |
| CONTEXT | D-06 | `ExitFrame` hard cap | Plan 01 + 02 |
| CONTEXT | D-09 | Manual-tracking-first event behavior | Plan 02 |

## Conclusion

Research supports a two-plan, sequential Phase 2:
- first make event lifetime trimming and cleanup instance-safe
- then narrow helper behaviors and delete paths so manual-tracking-first event segments stay bounded

The AOL reference is consistent with this direction and should be treated as the behavioral model for this phase.
