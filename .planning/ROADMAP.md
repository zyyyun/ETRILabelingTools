# Milestone v1.0 Roadmap

Last updated: 2026-07-22

## Summary

- Total phases: 5
- Requirements mapped: 16
- Milestone goal: stabilize Event Waypoint creation, exit handling, assisted tracking, and deletion behavior

## Phase 1: Normalize Event Creation And Exit Finalization

**Goal:** Make all event entry and exit actions resolve through one canonical event creation flow so a single user action produces a single event segment.

**Requirements:** `EVT-01`, `EVT-02`, `EVT-03`

**Success criteria:**

1. Exit button and `X` key use one shared event finalization path.
2. Exit-frame bbox drawing no longer creates a duplicate event waypoint.
3. Event creation no longer depends on vehicle overlap to decide whether to create extra waypoints or prompt unexpectedly.

## Phase 2: Clamp Event Lifetime And Cleanup Derived Artifacts

**Goal:** Ensure event visibility, interpolation, and cleanup all honor the event exit boundary and remove orphaned artifacts.

**Requirements:** `EVT-04`, `EVT-05`, `TRK-03`

**Success criteria:**

1. Event boxes do not persist beyond the confirmed exit frame.
2. Interpolation and auto-tracking do not extend event artifacts past the event segment.
3. Deleting an event removes undeletable residual waypoints and linked derived boxes.

## Phase 3: Stabilize Vehicle Coupling And Selection Safety

**Goal:** Eliminate vehicle duplication and wrong-target deletion caused by overlap workflows and stale list selection state.

**Requirements:** `TRK-01`, `TRK-02`, `UI-01`, `UI-02`, `UI-03`

**Success criteria:**

1. Event-assisted vehicle generation no longer duplicates entry or exit boxes for one logical object.
2. Vehicle identities stay stable instead of splitting into mismatched IDs such as `01` and `02`.
3. Waypoint deletion always targets the currently selected list item and empty-space clicks clear stale selections.

## Phase 4: Synchronize Event IDs Across Waypoint Segments

**Goal:** Treat `EventId` as a property of an Event Waypoint so changing an event in one frame updates every box in that waypoint while preserving the segment identity.

**Requirements:** `EVT-06`
**Depends on:** Phase 3

**Success criteria:**

1. Changing an event type updates every non-deleted event box in the same waypoint, regardless of per-frame rectangle changes.
2. The operation is scoped by `EventInstanceId` and never changes another event segment that happens to share an event type.
3. The Event Waypoint list and the current-frame event panel refresh to show the updated event type immediately.

### Phase 5: Align Event Waypoint Panel Columns

**Goal:** Make Event Waypoints use the same Entry, Exit, and Object columns as Person and Vehicle Waypoints.

**Requirements:** `UI-04`
**Depends on:** Phase 4

**Success criteria:**

1. The Event Waypoint list displays `Entry`, `Exit`, and `객체` columns in that order.
2. Entry and Exit use the same video-time formatting as the Person and Vehicle Waypoint lists, not JSON timestamps.
3. Each row retains the current event type and interacting-object information while remaining selectable for existing waypoint actions.

### Phase 6: Prevent stale event boxes after waypoint box edits

**Goal:** [To be planned]
**Requirements**: TBD
**Depends on:** Phase 5
**Plans:** 2/2 plans complete

Plans:

- [x] TBD (run /gsd-plan-phase 6 to break down) (completed 2026-07-23)

### Phase 7: Delete face and plate annotations by frame or waypoint

**Goal:** [To be planned]
**Requirements**: TBD
**Depends on:** Phase 7
**Plans:** 1/1 plans complete

Plans:

- [x] TBD (run /gsd-plan-phase 7 to break down) (completed 2026-07-23)

## Verify Phase

**Purpose:** Run the milestone-level PPT regression and UAT gate after all numbered feature phases are complete.

**Artifacts:** `.planning/verification/event-waypoint-regression/`

**Success criteria:**

1. Each reproduced PPT scenario is replayed and recorded as fixed, partial, or blocked with notes.
2. Ghost `contact`, orphaned event waypoints, and event lifetime behavior are explicitly checked.
3. The verification evidence remains reusable for later feature phases without changing numbered Phase ordering.
