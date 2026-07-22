# Milestone v1.0 Roadmap

Last updated: 2026-07-21

## Summary

- Total phases: 4
- Requirements mapped: 14
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

## Phase 4: Reproduce PPT Scenarios And Lock In Regression Coverage

**Goal:** Validate the reproduced field issues against a repeatable regression checklist and capture results for follow-up work.

**Requirements:** `QA-01`, `QA-02`, `QA-03`

**Success criteria:**
1. Each PPT bug scenario is replayed and marked fixed, partial, or blocked with notes.
2. Ghost `contact` or orphaned event waypoint creation is explicitly checked.
3. Regression results are documented in a milestone verification artifact for reuse in later changes.

## Next Up

**Phase 1: Normalize Event Creation And Exit Finalization**

Recommended next command once execution begins:

`$gsd-discuss-phase 1`
