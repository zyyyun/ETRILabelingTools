---
phase: 06-prevent-stale-event-boxes-after-waypoint-box-edits
plan: 01
subsystem: event-waypoint-propagation
tags: [event, waypoint, propagation, tdd, regression]
dependency_graph:
  requires: [EventWaypointUpdateHelper, TrackingIdentityHelper]
  provides: [EventWaypointBoxPropagationHelper.PlanPropagation]
  affects: [Phase 06 Plan 02 UI integration]
tech_stack:
  added: []
  patterns: [pure-planner, fail-closed-scope-resolution, identity-scoped-mutations]
key_files:
  created: [WinFormsApp1/Logic/EventWaypointBoxPropagationHelper.cs]
  modified: [WinFormsApp1.Tests/Program.cs]
decisions:
  - "Propagation is a pure update/addition plan with stable target references and no removal operation."
  - "Only identified EventInstanceId boxes can be propagated; ambiguous and legacy EventId-only inputs fail closed."
metrics:
  duration: "about 12 minutes"
  completed: "2026-07-23"
---

# Phase 06 Plan 01: Identity-scoped event rectangle propagation plan Summary

Created a fail-closed, pure event-box propagation planner that updates only the forward range of one EventInstanceId while preserving manual corrections and deletion tombstones.

## Tasks Completed

1. Added named Phase 6 console regressions for forward-only propagation, manual-frame precedence, tombstone preservation, instance isolation, ambiguous scopes, and no deletion operations.
2. Added `EventWaypointBoxPropagationHelper`, returning immutable update/addition records without mutating boxes or waypoints.

## Verification

- RED: `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` failed as expected with CS0103/CS0246 because the propagation helper did not yet exist.
- GREEN: `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` passed all 61 tests.
- `dotnet build ETRILabelingTool.sln -c Debug -p:Platform=x64 --no-restore` succeeded with 0 errors (306 existing warnings).

## Decisions Made

- The planner resolves an active event scope before planning and rejects deleted, non-event, no-instance, invalid-range, and ambiguous inputs.
- A tombstone at one target frame blocks replacement only at that frame; later eligible frames remain planned.

## Deviations from Plan

None - plan executed exactly as written.

## Self-Check: PASSED

- `WinFormsApp1/Logic/EventWaypointBoxPropagationHelper.cs` exists.
- Task commits `68a5d0d` and `3f862fb` exist.
