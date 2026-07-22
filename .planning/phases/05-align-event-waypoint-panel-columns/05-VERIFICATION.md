---
phase: 05-align-event-waypoint-panel-columns
verified: 2026-07-22T16:55:00+09:00
status: human_needed
score: 4/5 must-haves verified
---

# Phase 5: Event Waypoint Panel Columns Verification Report

## Observable Truths

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | Event list columns are Entry, Exit, 객체 in Person/Vehicle order. | VERIFIED | Designer uses widths 80, 80, 95 with no Event column. |
| 2 | Event Object displays the event name. | VERIFIED | `EventWaypointListRowHelper` test passes. |
| 3 | Event Entry and Exit use video-time strings, not imported timestamps. | VERIFIED | Row helper uses only `EntryTime` and `ExitTime`; 52-test harness passes. |
| 4 | InteractingObject remains data-only and JSON-compatible. | VERIFIED | Waypoint model and JSON paths are unchanged; row test proves it is not displayed. |
| 5 | Event rows remain selectable and deletable in the running UI. | NEEDS HUMAN | Item tags and existing click/delete handlers remain, but WinForms interaction requires manual confirmation. |

## Requirement Coverage

| Requirement | Status | Evidence |
|---|---|---|
| UI-04 | SATISFIED (automated) | Three-column layout and row-value contract are covered by source inspection and harness tests. |

## Validation

- x64 Debug build: passed, 0 errors (existing warnings remain).
- Test harness: passed, 52/52.

## Human Verification Deferred

The user requested milestone-wide verification after all feature phases. In
Verify Phase, confirm the Event panel visually matches Person/Vehicle and a
row can be selected then deleted without opening an inline editor.
