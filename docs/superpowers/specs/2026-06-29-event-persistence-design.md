# Event Persistence Design

## Goal
Fix the Event labeling persistence bug where Event data appears and disappears after save/reload, while preserving compatibility with the existing JSON structure used by this tool and other downstream tools.

## Scope
This design covers Event persistence only.
It does not implement the Entry/Exit UI split, interpolation backend fixes, or auto-labeling freeze fixes.

## Problem Summary
The current tool allows multiple Event instances of the same Event type, such as multiple `contact` segments in one video.
However, several code paths still identify Events primarily by `EventId`, which represents the Event category/type rather than a unique instance.
Because of that, different Event instances can be merged, overwritten, or restored incorrectly during save/load and waypoint operations.

The most likely failure mode is:
- multiple Event instances share the same `EventId`
- save logic serializes them without a stable per-instance identity
- load logic reconstructs waypoints and boxes using weak grouping rules
- Event instances collapse together or partially overwrite each other
- the user sees Event data appear, disappear, or reconnect incorrectly after reload

## Requirements

### Functional Requirements
- Multiple Event instances of the same Event type must remain distinct.
- Saving and reloading must preserve each Event instance without collapsing separate segments.
- Existing JSON consumers must remain compatible.
- Older JSON files without the new Event instance identifier must still load.
- After an older file is re-saved, the new stable identity should be present.

### Compatibility Requirements
- Existing JSON fields such as `category_id`, `track_id`, `track_info`, and `interacting_object` must remain unchanged.
- A single additional optional field may be added when needed.
- Other tools that ignore unknown fields must still be able to read the JSON.

### Non-Goals
- No redesign of the external JSON schema beyond one optional field.
- No migration of Person or Vehicle identity rules in this phase.
- No interpolation or auto-labeling redesign in this phase.

## Recommended Approach
Use the existing JSON structure and add one optional tool-specific field: `event_instance_id`.

This field will be the stable identity for one Event instance across:
- Event bounding boxes
- Event waypoint ranges
- save/reload cycles
- edit operations such as Exit adjustment or Event termination

The tool will treat `event_instance_id` as the primary identity for Event instance grouping.
The existing `EventId` will remain the Event type/category identifier only.

## Alternatives Considered

### Option 1: Keep JSON unchanged and improve only internal grouping
Pros:
- zero schema change
- safest for external readers

Cons:
- reload must infer identity from weak signals such as type, Entry/Exit, interacting object, and trajectory
- ambiguous cases remain difficult to resolve correctly
- regression risk stays high

### Option 2: Add `event_instance_id` while preserving all existing fields
Pros:
- strong instance identity
- minimal schema change
- straightforward backward compatibility
- external readers can ignore the new field safely

Cons:
- requires a small schema extension
- requires load fallback for legacy files

### Option 3: Redesign Event serialization completely
Pros:
- cleanest long-term model

Cons:
- highest migration cost
- unnecessary breakage for downstream consumers

Recommended option: Option 2.

## Data Model Changes

### AnnotationData
Add an optional field:
- `event_instance_id`

Behavior:
- present only for Event annotations
- omitted for Person and Vehicle annotations
- omitted only when exporting older data is explicitly required, otherwise written by default

### BoundingBox
Add an internal property:
- `EventInstanceId`

Behavior:
- used only when `Label == "event"`
- propagated when Event boxes are copied, interpolated, retracked, or extended

### WaypointMarker
Add an internal property:
- `EventInstanceId`

Behavior:
- used only for Event waypoints
- the primary identity for an Event waypoint inside the tool

## Identity Rules

### Event Type vs Event Instance
- `EventId` continues to mean Event type such as contact/exchange/board/final_exchange/throw.
- `EventInstanceId` means one distinct occurrence of an Event in the timeline.

### Distinct Event Instances
Events must be treated as distinct when any of the following differ materially:
- Entry/Exit range
- interacting object
- bbox trajectory/position over frames

In practice, the tool should not try to merge Event instances once `EventInstanceId` exists.

## Save Pipeline Design
When exporting annotations:
1. Determine the Event waypoint or Event instance associated with each Event bounding box.
2. Preserve existing exported values for `category_id`, `track_id`, `track_info`, and `interacting_object`.
3. Add `event_instance_id` for Event annotations.
4. Ensure every Event annotation belonging to the same Event instance writes the same `event_instance_id`.
5. Never use `EventId` alone to infer Event instance grouping during export.

### Important Rule
`track_id` remains backward-compatible and should not be repurposed into a unique Event instance key if downstream tools already interpret it differently.
The new instance key must remain separate.

## Load Pipeline Design
When loading annotations:
1. If an Event annotation contains `event_instance_id`, use it as the primary grouping key.
2. Reconstruct Event boxes and Event waypoints by `event_instance_id` first.
3. If `event_instance_id` is missing, treat the file as legacy.
4. For legacy files, reconstruct provisional Event instances using:
   - Event type
   - track entry/exit range
   - interacting object
   - bbox continuity / trajectory proximity
5. After reconstructing a legacy file in memory, assign fresh internal `EventInstanceId` values.
6. On the next save, export those values as `event_instance_id`.

## In-Memory Consistency Rules
Event operations must update the following together as one logical unit:
- Event bounding boxes
- Event waypoint
- EventInstanceId linkage

Any Event edit path that updates only one of those is invalid.

### Commit Rules
- Entry selection alone does not finalize an Event instance.
- Exit confirmation finalizes or updates the Event instance.
- Exit shortening, Event termination, propagation, and retracking must operate by `EventInstanceId` first.
- UI selection and list rendering must not identify Event instances by `EventId` alone.

## Code Areas To Change
The implementation should focus on these areas in `WinFormsApp1/Form1.cs`:
- Event bounding box creation and cloning
- waypoint creation/update paths around Entry/Exit handling
- Event termination logic
- Event propagation / interpolation / retracking paths
- JSON export path where annotations are created
- JSON load path where temp bounding boxes and temp waypoints are reconstructed
- any lookup helper such as `FindWaypointForBox` that currently relies on weak Event identity

## Error Handling
- If duplicate `event_instance_id` values conflict across incompatible Event ranges, log a warning and split them into separate in-memory Event instances.
- If an Event annotation is missing `track_info`, load the bbox but mark waypoint reconstruction as incomplete.
- If a legacy file cannot be grouped unambiguously, prefer preserving separate Event groups rather than merging them aggressively.
- Unknown extra JSON fields must continue to be ignored safely.

## Validation Plan

### Scenario 1: Same Event type repeated
- Create two `contact` Events in separate frame ranges.
- Save and reload.
- Both Event instances must remain separate.

### Scenario 2: Same Event type with different interacting objects
- Create two Events of the same type with different `interacting_object` values.
- Save and reload.
- Each Event must retain its own interacting object and waypoint.

### Scenario 3: Exit adjustment
- Shorten one Event's Exit range.
- Save and reload.
- Only that Event instance should change.
- Other same-type Event instances must remain intact.

### Scenario 4: Legacy JSON
- Load an older file without `event_instance_id`.
- Verify Events still appear correctly.
- Save and reload.
- Verify the file now behaves stably with explicit Event instance IDs.

## Risks
- Legacy file grouping may still have edge cases if historical data is already ambiguous.
- Some downstream tools may validate against a strict schema and reject unknown fields.
- Event logic may still fail if a few remaining lookup paths continue using `EventId` only.

## Mitigations
- Keep the added field optional and non-breaking.
- Document the new field clearly for downstream tool owners.
- Centralize Event lookup helpers so Event identity rules are implemented once.
- Prefer preserving too many Event groups over accidentally merging different ones.

## Rollout Notes
- Default behavior should export `event_instance_id` for new saves.
- No manual migration step is required.
- Legacy files should be upgraded passively on first save.

## Open Decision Resolved In This Spec
Adopt Option 2:
- keep the existing JSON structure
- add one optional field `event_instance_id`
- use it as the stable Event instance identity in memory and across save/reload