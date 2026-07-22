# Phase 4 PPT Scenario Catalog

**Source PPT:** `260720_툴 수정 사항.pptx`  
**Prepared:** 2026-07-22  
**Purpose:** Map each original PPT issue to a repeatable verification scenario while preserving the original numbering.

## PPT Items

| PPT Item | Short Title | Primary Workflow | Related Phase(s) | Grouped Scenario |
|----------|-------------|------------------|------------------|------------------|
| 1 | Duplicate event waypoint creation | Event finalization | 1 | G1 |
| 2 | Event interacting object editor lands on exit column | Event waypoint list editing | 1 / 4 | G5 |
| 3 | Event box persists beyond exit | Event lifetime | 2 | G2 |
| 4 | Entry + `E/X` path creates one waypoint but lifetime bug remains | Event finalization and persistence | 1 / 2 | G1, G2 |
| 5 | Vehicle overlap alone triggers waypoint confirm behavior | Event-assisted vehicle overlap | 3 | G3 |
| 6 | No exit-frame bbox keeps single event waypoint | Baseline safe event finalization | 1 | G1 |
| 7 | Frame-unit deletion auto-removes undeletable event waypoint | Event delete/cleanup | 2 | G4 |
| 8 | Event waypoint persists until overlapped object exit and cannot be deleted | Event lifetime and manual-tracking-first behavior | 2 | G2, G4 |
| 9 | Stale person selection deletes wrong waypoint | Waypoint selection ownership | 3 | G5 |
| 10 | Exit-frame bbox + auto track duplicates entry vehicle boxes | Event-assisted vehicle duplication | 3 | G3 |
| 11 | Same vehicle splits into `01/02` identities | Vehicle identity stability | 3 | G3 |
| 12 | Exit-frame bbox + auto track creates duplicate event waypoint and duplicate vehicle tracking | Event + vehicle duplication | 1 / 3 | G1, G3 |
| 12-2 | Motorcycle/bicycle variant still duplicates with fixed first frame | Event-assisted vehicle duplication on non-YOLO-friendly classes | 3 | G3 |
| 13 | Ghost `contact` waypoint appears and persists until person objects vanish | Ghost/orphan waypoint cleanup | 2 / 4 | G4 |

## Grouped Scenarios

### G1. Event Finalization Uniqueness

**Covers:** `1`, `4`, `6`, `12`  
**Intent:** Verify that event creation/finalization routes produce exactly one event segment per action.  
**Key expectation:** `Entry` is temporary, `Exit/X` is the only finalization point, and exit-frame bbox does not spawn a second event waypoint.

### G2. Event Lifetime Hard Cap

**Covers:** `3`, `4`, `8`  
**Intent:** Verify that event boxes disappear immediately after `ExitFrame` and do not persist until a parent object ends.  
**Key expectation:** Event lifetime is bounded to the finalized waypoint range everywhere: UI, memory, and export.

### G3. Vehicle Independence And Duplicate Prevention

**Covers:** `5`, `10`, `11`, `12`, `12-2`  
**Intent:** Verify that event workflows do not split or duplicate vehicles.  
**Key expectation:** Vehicles retain one internal identity via `VehicleInstanceId`, event workflows reuse an existing matched body only, and no new vehicle bbox is auto-created as a side effect.

### G4. Event Cleanup And Ghost Waypoint Removal

**Covers:** `7`, `8`, `13`  
**Intent:** Verify that deleting or terminating an event removes the correct instance-scoped artifacts and does not leave orphan `contact` or ghost event waypoints behind.  
**Key expectation:** Cleanup is `EventInstanceId`-scoped and persistent ghost waypoints do not remain.

### G5. Selection Ownership And Event List Editing

**Covers:** `2`, `9`  
**Intent:** Verify that event list editing targets the intended field and that delete/edit actions honor the currently active list.  
**Key expectation:** Interacting-object editing lands on the object column only, and stale selection from another list cannot delete the wrong waypoint.

## Verification Order

Recommended execution order:

1. `G1` Event Finalization Uniqueness
2. `G2` Event Lifetime Hard Cap
3. `G3` Vehicle Independence And Duplicate Prevention
4. `G4` Event Cleanup And Ghost Waypoint Removal
5. `G5` Selection Ownership And Event List Editing

## Notes

- The grouped scenarios are optimization layers over the original PPT numbering, not replacements for it.
- Every final report row must still cite the original PPT item number even when executed as part of a grouped scenario.
