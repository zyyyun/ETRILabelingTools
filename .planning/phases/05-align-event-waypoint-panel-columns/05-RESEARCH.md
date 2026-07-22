# Phase 5 Research: Event Waypoint Panel Columns

## Current State

- `Form1.Designer.cs` defines Person and Vehicle lists as `Entry` (80),
  `Exit` (80), and `객체` (95), while Event uses `Event`, `Entry`, `Exit`,
  and `객체`.
- `UpdateWaypointListView()` replaces the Event row's first item with its event
  name, then displays a JSON/subtitle timestamp and `InteractingObject`.
- `Form1.Timeline.cs` opens a `TextBox` for Event list subitem index 2 on both
  double-click and mouse-up, persisting the value to `InteractingObject`.
- JSON import/export retains `interacting_object` independently of list
  rendering, so hiding the UI column does not require schema changes.

## Recommended Approach

1. Make the Event `ListView` definition exactly match the Person/Vehicle
   three-column shape and widths.
2. Build Event rows from `waypoint.EntryTime`, `waypoint.ExitTime`, and the
   resolved event display name, in that order.
3. Remove or inert the Event-list inline editing triggers so a hidden column
   cannot create an editor or mutate `InteractingObject`.
4. Extract the row-value calculation into a non-UI helper only if needed to
   give the existing lightweight harness direct regression coverage.

## Risks And Guards

- Event list selection and deletion use `ListViewItem.Tag`, not subitem index;
  preserve the tag and handlers.
- Do not remove `InteractingObject` from `WaypointMarker` or JSON models.
- Avoid using `frameTimestampMap` or subtitle timestamps for Event Entry/Exit.

## Validation

- Assert the Event list has only Entry, Exit, Object columns with the same
  widths as Person/Vehicle.
- Assert an Event row uses waypoint video-time strings and event name, not
  `InteractingObject` or imported timestamps.
- Build the x64 Debug solution and run the test harness.
