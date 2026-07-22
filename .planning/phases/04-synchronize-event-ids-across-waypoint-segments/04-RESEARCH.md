---
phase: 04
slug: synchronize-event-ids-across-waypoint-segments
status: complete
created: 2026-07-22
---

# Phase 4 Research: Synchronize Event IDs Across Waypoint Segments

## Current Behavior

- `Form1.Drawing.cs` updates the selected event box, then finds related boxes
  by old `EventId`, identical rectangle, and the frame range of the first
  waypoint containing the selected frame.
- The rectangle equality check is the direct cause of a manually adjusted
  box preventing the event type from reaching the remaining frames.
- `TrackingIdentityHelper.MatchesWaypoint` already treats a non-empty event
  `EventInstanceId` as the authoritative waypoint identity. Its numeric
  fallback uses `EventId`, which is unsafe after an event type is changed.
- Event propagation intentionally skips events in manual-tracking-first mode;
  this phase must not alter that guard or generate new boxes.

## Implementation Shape

1. Extract a small testable helper that resolves active event boxes belonging
   to the selected waypoint.
2. Resolve the waypoint by matching `EventInstanceId` when both the selected
   box and marker contain it. Never use rectangle equality.
3. For imported legacy boxes without an instance id, use the waypoint frame
   range plus the selected box's pre-change `EventId`; if this cannot identify
   one segment conservatively, make no cross-waypoint update.
4. Update only `!IsDeleted` event boxes, preserve each box's rectangle and
   `EventInstanceId`, and refresh the waypoint list and current-frame panel.

## Undo Constraint

`UndoActionType.ModifyBox` stores a single prior identity and its undo lookup
uses `GetBoxId`, so it cannot restore multiple boxes after their `EventId`
changes. The existing `TrackedBoxes` shape is used for grouped operations but
currently models add/remove behavior. Add a dedicated grouped event-id change
snapshot, or extend the undo action with before/after box snapshots and an
explicit undo/redo branch. One user selection must create exactly one undo
entry and restore every changed active box.

## Test Strategy

The project uses a lightweight executable test harness in
`WinFormsApp1.Tests/Program.cs`. Put waypoint-membership and event-id update
logic in a non-UI helper so it can be tested without constructing `Form1`.

Required cases:

- Same `EventInstanceId` updates all active boxes despite different rectangles.
- A second waypoint sharing the former `EventId` is unchanged.
- Soft-deleted boxes are unchanged.
- Legacy boxes without instance ids update only within the selected waypoint
  frame range and prior type.
- The result preserves `EventInstanceId` and reports the changed boxes needed
  to build a single undo snapshot.

## Validation Architecture

- Build and run `WinFormsApp1.Tests` after helper and UI integration changes.
- Manually verify a changed event type appears in both the Event Waypoint list
  and current-frame Event panel. PPT/UAT replay remains deferred to the
  numberless Verify Phase.

## Risks

- Selecting a waypoint only by frame range is ambiguous when legacy event
  segments overlap. The legacy fallback must retain the prior `EventId` and
  refuse a broader match rather than mutate an unrelated segment.
- Undo/redo must find boxes by stable reference or complete snapshots; looking
  them up by the changed `EventId` will fail after the mutation.

## Files Likely To Change

- `WinFormsApp1/Forms/Form1.Drawing.cs`
- `WinFormsApp1/Forms/Form1.Undo.cs`
- `WinFormsApp1/Form1.cs`
- `WinFormsApp1/Logic/TrackingIdentityHelper.cs` or a new focused event helper
- `WinFormsApp1.Tests/Program.cs`

## Research Conclusion

The change is a contained event-waypoint identity and grouped-history fix. It
should be implemented as a reusable non-UI operation with the combo-box event
handler as the only UI caller, rather than expanding the existing rectangle
based predicate.

## RESEARCH COMPLETE
