---
status: ready_for_manual_uat
scope: phase-04, phase-05, phase-06, phase-08, gsd-quick
source:
  - 04-PPT-VERIFICATION.md
  - ../../phases/04-synchronize-event-ids-across-waypoint-segments/04-VERIFICATION.md
  - ../../phases/05-align-event-waypoint-panel-columns/05-VERIFICATION.md
  - ../../phases/06-prevent-stale-event-boxes-after-waypoint-box-edits/06-VALIDATION.md
  - ../../phases/07-delete-face-and-plate-annotations-by-frame-or-waypoint/07-VERIFICATION.md
---

# Milestone And Quick Change UAT Scenarios

## Purpose

This is the single manual UAT script for the changes recorded as blocked or
human-needed in `04-PPT-VERIFICATION.md`, the later Phase 6 and Phase 8
features, and the quick changes. Record results only in
`MILESTONE-UAT-CHECKLIST.md`.

`Phase 08` is stored in the renumbered `07-*` planning directory; this document
uses the original Phase 8 name used by its verification report.

## Common Setup

1. Start the x64 Debug application with a video containing person, vehicle,
   event, face, and plate annotations where applicable.
2. Prepare one Event waypoint with at least three non-deleted boxes on
   different frames, different rectangles, and an `EventInstanceId`.
3. Prepare another Event waypoint of the same original type but with a
   different `EventInstanceId`.
4. Keep a legacy JSON export containing `event_exchange`, `event_board`,
   `event_disembark`, and `event_camouflage` when available.

## UAT-01: PPT Event Type Synchronization (Phase 4)

**PPT source:** PPT 3 event-name update finding.

1. Select one event box in the prepared waypoint and change its event type.
2. Navigate through every frame of that waypoint.
3. Inspect the Event waypoint row and current-frame Event panel.
4. Inspect the other waypoint sharing the old type.
5. Press Undo once, then Redo once.

**Expected:** Every active box in the selected waypoint changes type despite
rectangle differences. The other EventInstanceId remains unchanged. The row and
Event panel refresh immediately. One Undo restores all changed boxes; one Redo
reapplies all of them.

## UAT-02: PPT Event Waypoint Panel And Selection (Phase 5 + quick)

**PPT source:** PPT 2 and PPT 9; quick commit `bf1c457`.

1. Verify the Event list columns are `Entry`, `Exit`, and `Object` in that order.
2. Confirm Entry and Exit are video times, not imported JSON timestamps.
3. On a row with different entry and exit frames, click Entry, Exit, then
   Object.
4. Double-click only the Object cell and edit its interacting-object value.
5. Select a Person waypoint, then select an Event or Vehicle waypoint and use
   the selected-waypoint delete action.
6. Click empty space in the waypoint area, then invoke delete again.

**Expected:** Entry moves to EntryFrame; Exit moves to ExitFrame; Object keeps
the existing entry-frame box selection behavior. Inline editing opens only for
Object, never Exit. Deletion targets the active Event/Vehicle selection, and
empty-space selection cannot delete a stale Person waypoint.

## UAT-03: PPT Event Lifetime And Cleanup Regression

**PPT source:** PPT 1, 3, 4, 6, 7, 8, 12, and 13.

1. Create and finalize an event through Entry then Exit/X, once with an
   exit-frame box and once without one.
2. Confirm exactly one Event waypoint is created for each action.
3. Move to ExitFrame and then ExitFrame + 1.
4. Delete every event box in one frame through the frame-level deletion flow.
5. Create an overlapping-object case and confirm the event does not persist to
   the other object's exit.

**Expected:** No duplicate Event waypoint is created; event boxes disappear
immediately after their own ExitFrame; frame-level cleanup removes related
event artifacts without leaving a ghost `contact` waypoint; object overlap
does not extend event lifetime or make the event undeletable.

## UAT-04: Event Rectangle Propagation And History (Phase 6)

1. In one EventInstanceId, resize or drag an event box on a source frame.
2. Inspect later derived boxes in the same waypoint and a later manually
   adjusted box.
3. Inspect a same-type event with a different EventInstanceId.
4. Delete one event box on a single frame, then inspect the next frame.
5. Undo and Redo each edit once.

**Expected:** Only eligible forward derived boxes of the same EventInstanceId
update. Earlier boxes, manually adjusted later boxes, other instances, and
deleted tombstones remain untouched. Single-frame deletion does not delete a
later box. One Undo/Redo restores/reapplies the complete drag or resize change.
The canvas, Event list, and waypoint list refresh without frame navigation.

## UAT-05: Face And Plate Deletion (Phase 8)

1. Select a face on one frame and press `G`; inspect adjacent frames and the
   parent person body.
2. Select a plate on one frame and press `G`; inspect adjacent frames and the
   parent vehicle body.
3. Select a face within a parent waypoint and press Delete; repeat for a plate.
4. Inspect a different person's face, a different vehicle's plate, body boxes,
   and waypoint rows.
5. Undo each deletion, export JSON, and inspect exported child links.

**Expected:** `G` deletes only the selected current-frame child. Delete removes
only matching same-subtype children in the resolved parent waypoint. Other
children, bodies, and waypoint markers remain. Undo restores the intended
children. Export omits deleted child annotations/links while retaining active
body annotations.

## UAT-06: Quick UI Text Recovery

1. Select a labeled person, vehicle, and event in turn.
2. Inspect Object Info, waypoint labels, and attribute labels.

**Expected:** Korean UI text is readable with no replacement question marks or
mojibake in the inspected controls.

## UAT-07: Quick Event Catalog And Legacy JSON Migration

1. Open the Event label dropdown.
2. Confirm this exact order: `event_contact`, `event_throw`,
   `event_final_exchange`, `event_get on`, `event_get off`, `event_suspect`,
   `event_controlled_delivery`, `event_camouflage`.
3. Save a new JSON and inspect event category names and IDs.
4. Load the legacy JSON, inspect event labels, save to a new file, and reopen
   it.

**Expected new IDs:** 25 contact, 26 throw, 27 final_exchange, 28 get on,
29 get off, 30 suspect, 31 controlled_delivery, 32 camouflage.

**Expected legacy mapping:**

| Legacy | New saved value |
|---|---|
| `event_exchange` | `event_throw` |
| `event_board` | `event_get on` |
| `event_disembark` | `event_get off` |
| `event_camouflage` | `event_camouflage` |
| `event_throw` | `event_throw` |

Category names must take precedence over legacy numeric category positions
when loading a JSON that includes `categories[].name`.

## Evidence To Attach

- Screenshots or frame numbers for each UAT section.
- A before/after JSON excerpt for UAT-07.
- For every failure: video name, frame range, selected object/event instance,
  exact action, expected result, and actual result.
