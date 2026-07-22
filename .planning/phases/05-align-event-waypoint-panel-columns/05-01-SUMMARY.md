---
phase: 05
plan: 01
subsystem: event-waypoint-ui
tags: [winforms, waypoint, event, ui]
requires:
  - phase: 04
    provides: identity-safe Event Waypoint display lookup
provides:
  - Event Waypoint Entry/Exit/Object three-column rendering
  - video-time Event row values independent of JSON timestamps
  - hidden but JSON-compatible interacting-object metadata
affects: [verify-phase, event-waypoint-regression]
key-files:
  created:
    - WinFormsApp1/Logic/EventWaypointListRowHelper.cs
  modified:
    - WinFormsApp1/Form1.Designer.cs
    - WinFormsApp1/Forms/Form1.Drawing.cs
    - WinFormsApp1.Tests/Program.cs
requirements-completed: [UI-04]
completed: 2026-07-22
---

# Phase 5 Plan 01: Event Waypoint Column Alignment Summary

**Event Waypoints now use the same Entry, Exit, and Object presentation as
Person and Vehicle Waypoints.**

## Accomplishments

- Replaced the four Event columns with `Entry` (80), `Exit` (80), and `객체`
  (95), matching Person and Vehicle lists.
- Event rows now use `WaypointMarker.EntryTime`, `ExitTime`, and resolved
  event name; JSON/subtitle timestamps and InteractingObject are not rendered.
- Removed the active MouseUp hook for the old inline interacting-object editor.
- Preserved `InteractingObject` data and existing JSON import/export paths.
- Added a row helper and regression test; harness now passes 52 tests.

## Validation

- `dotnet build ETRILabelingTool.sln -c Debug -p:Platform=x64` passed with 0 errors.
- `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj` passed: 52/52.

## Remaining Work

- Confirm in WinForms that Event rows visually match Person/Vehicle columns and
  remain selectable/deletable. This is deferred to the milestone Verify Phase.
