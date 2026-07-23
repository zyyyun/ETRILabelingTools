---
phase: 06-prevent-stale-event-boxes-after-waypoint-box-edits
plan: 02
subsystem: event-waypoint-winforms-integration
tags: [winforms, event, waypoint, propagation, undo-redo, refresh]
dependency_graph:
  requires:
    - phase: 06-01
      provides: "Identity-scoped EventWaypointBoxPropagationHelper.PlanPropagation planner"
  provides:
    - "Atomic event rectangle propagation undo/redo"
    - "Immediate Event list and waypoint list refresh after event edits and frame-local deletes"
  affects: [phase-07-tracked-object-synchronization, milestone-verify-phase]
tech_stack:
  added: []
  patterns: [stable-reference-batch-history, fail-closed-propagation-application, shared-event-ui-refresh]
key_files:
  created: [WinFormsApp1/Logic/EventRectanglePropagationUndoHelper.cs]
  modified:
    - WinFormsApp1/Form1.cs
    - WinFormsApp1/Forms/Form1.Undo.cs
    - WinFormsApp1/Forms/Form1.EventPropagation.cs
    - WinFormsApp1/Forms/Form1.Drawing.cs
    - WinFormsApp1.Tests/Program.cs
key-decisions:
  - "Defer the live WinForms smoke test to the milestone-wide Verify Phase; it is not approved by this plan."
  - "Treat the completed automated harness and Debug build as verification only for their covered behavior."
requirements-completed: [D-01, D-02, D-03, D-04, D-05, D-06]
metrics:
  duration: "checkpoint continuation"
  completed: "2026-07-23"
---

# Phase 06 Plan 02: Event Waypoint WinForms integration Summary

**Identity-scoped event rectangle propagation now reverses as one stable-reference batch and refreshes Event UI surfaces immediately after edits or frame-local deletes.**

## Tasks Completed

1. Applied the propagation planner as one reversible `EventRectanglePropagation` undo/redo transaction; completed in `6e6280a`.
2. Refreshed the canvas, Event list, and Event Waypoint list after event edits and frame-local deletes; completed in `e313fe8`.
3. Deferred the live WinForms smoke test to the milestone-wide Verify Phase at the user's explicit direction. This manual verification was **not run and did not pass** in this plan.

## Verification

Automated checks passed:

- `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore`
- `dotnet build ETRILabelingTool.sln -c Debug -p:Platform=x64 --no-restore` — succeeded with 0 errors and 308 existing warnings.

## Deferred Manual UI Verification

The following smoke checks are pending for the milestone-wide Verify Phase. They are explicitly deferred, not passed.

- **D-01:** Edit an event box on a middle frame and confirm propagation begins at that frame and continues only through the containing Event Waypoint exit.
- **D-02:** Confirm frames before the edited frame remain unchanged.
- **D-03:** Manually correct a later frame, edit an earlier frame again, and confirm the later correction remains intact; also confirm a different `EventInstanceId` is unchanged.
- **D-04:** Delete one event box with the frame-local control; confirm it vanishes only on that frame, remains absent after a preceding-frame edit, and later boxes remain.
- **D-05:** Delete the Event Waypoint through its existing waypoint action and confirm that this, rather than the frame-local delete, removes the full segment.
- **D-06:** Without navigating after each event edit or frame-local delete, confirm the canvas, Event list, and selected Event Waypoint row immediately show the current state; use one Undo and one Redo to confirm the entire propagation batch reverses and reapplies together.

## Task Commits

1. `6e6280a` — `feat(06-02): apply reversible event rectangle propagation`
2. `e313fe8` — `fix(06-02): refresh event UI after edits and deletes`

## Decisions Made

- The manual UI acceptance gate is deferred to the milestone-wide Verify Phase per the user's explicit instruction; no manual approval is claimed here.

## Deviations from Plan

### Deferred Verification

- **Task 3:** The plan required a live WinForms smoke test before completion. The user explicitly deferred all manual UI verification to the milestone-wide Verify Phase. No application source was changed for this deferral.

## Known Stubs

None found in the files created or modified by this plan.

## Next Phase Readiness

Phase 7 can build on the committed Phase 6 event rectangle behavior. The six D-01 through D-06 live-flow checks remain a required milestone-wide Verify Phase activity.

## Self-Check: PASSED

- `06-02-SUMMARY.md` exists.
- Task commits `6e6280a` and `e313fe8` exist.
