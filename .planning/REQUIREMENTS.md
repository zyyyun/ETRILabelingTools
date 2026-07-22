# Milestone v1.0 Requirements

Last updated: 2026-07-21

## Event Lifecycle

- [ ] **EVT-01**: User can create an event waypoint from entry and exit actions and the tool creates exactly one event segment for that action.
- [ ] **EVT-02**: User can end an event with the `Exit` button or `X` key and both paths finalize the same event lifecycle logic.
- [ ] **EVT-03**: User can draw an event bbox on the exit frame without the tool creating a second event waypoint.
- [ ] **EVT-04**: User can view an event only within its confirmed entry and exit range, and the event bbox disappears after the exit frame.
- [ ] **EVT-05**: User can delete an event waypoint and all derived event artifacts for that segment are removed together.

## Tracking And Identity

- [ ] **TRK-01**: User can create or propagate an event over an overlapped vehicle without the tool duplicating vehicle boxes at entry or exit.
- [ ] **TRK-02**: User can continue labeling a vehicle touched by an event workflow and the tool preserves one consistent vehicle identity instead of splitting it into multiple IDs.
- [ ] **TRK-03**: User can use interpolation or auto-tracking with an event workflow and generated boxes stay clamped to the intended event segment instead of extending until the parent object ends.

## UI State And Deletion Safety

- [ ] **UI-01**: User can switch between person, vehicle, and event waypoint lists and the current selection state always reflects the visible target list.
- [ ] **UI-02**: User can click empty space in a waypoint list to clear the active selection before issuing another action.
- [ ] **UI-03**: User can delete a selected vehicle or event waypoint while a person waypoint had been selected earlier and the tool deletes only the current target.

## Regression Verification

- [ ] **QA-01**: Maintainer can replay each reproduced July 20, 2026 PPT scenario and confirm whether the tool now produces the intended single event lifecycle.
- [ ] **QA-02**: Maintainer can verify that no ghost `contact` or orphaned event waypoint remains after event cleanup.
- [ ] **QA-03**: Maintainer can document the validation results so future event changes can be checked against the same regression set.

## Future Requirements

- Structural extraction of event and waypoint logic out of the main form
- Broader assisted-labeling UX cleanup beyond the reproduced stability issues
- Expanded automated tests around timeline and labeling state transitions

## Out Of Scope

- Face, plate, or other subordinate box workflows
- Non-event JSON schema redesign
- New event taxonomy or category expansion unrelated to the reproduced bugs
- Full UI redesign of the waypoint panels

## Traceability

| Requirement | Phase |
|-------------|-------|
| EVT-01 | 1 |
| EVT-02 | 1 |
| EVT-03 | 1 |
| EVT-04 | 2 |
| EVT-05 | 2 |
| TRK-01 | 3 |
| TRK-02 | 3 |
| TRK-03 | 2 |
| UI-01 | 3 |
| UI-02 | 3 |
| UI-03 | 3 |
| QA-01 | 4 |
| QA-02 | 4 |
| QA-03 | 4 |
