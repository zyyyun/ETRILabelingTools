# Phase 8: Delete Face And Plate Annotations By Frame Or Waypoint - Context

**Gathered:** 2026-07-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Provide safe, scope-aware deletion for subordinate `face` and `plate`
annotations. `G` removes only the selected subordinate box in the current
frame; `Delete` removes matching subordinate boxes for the selected parent
person or vehicle waypoint. Parent body boxes and their waypoints remain
intact, and saved JSON must not retain links to removed child annotations.

</domain>

<decisions>
## Implementation Decisions

### Shortcut scope
- **D-01:** The new `G` and `Delete` rules apply only when the selected box is
  a `face` or `plate` box.
- **D-02:** `G` deletes only the selected face or plate box in the current
  frame. It must not affect the same child object in other frames.
- **D-03:** Selecting a person or vehicle body preserves the existing delete
  behavior; Phase 8 must not repurpose body deletion into child deletion.

### Waypoint-wide child deletion
- **D-04:** `Delete` removes every face or plate box associated with the
  current parent person or vehicle waypoint.
- **D-05:** Membership is determined by both the parent waypoint frame range
  and the child-to-parent linked identity, so another person's face or another
  vehicle's plate is never removed merely because it appears in the same
  frames.
- **D-06:** Deleting child boxes must not delete, alter, or end the parent body
  box or its person/vehicle waypoint.

### JSON link integrity
- **D-07:** A saved JSON export omits each deleted face/plate annotation and
  removes its corresponding `FaceLinks` or `PlateLinks` record.
- **D-08:** `body_annotation_id` and its parent body annotation are retained;
  only the removed child annotation ID and its link record are cleaned up.

### the agent's Discretion
- Preserve the existing undo and deleted-box history conventions while ensuring
  deleted subordinate boxes and stale child links do not reappear in JSON.
- Choose the smallest reusable helper and regression coverage for resolving a
  child box's parent waypoint and filtering face/plate exports.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone and prior decisions
- `.planning/ROADMAP.md` - Phase 8 scope, dependency order, and Verify Phase
  timing. Phase 8 is the explicit addition for face/plate deletion despite the
  earlier milestone-level out-of-scope note.
- `.planning/PROJECT.md` - Labeling-stability priority and preservation of
  existing working-tree changes.
- `.planning/STATE.md` - Current milestone handoff state and Verify Phase
  deferral.
- `.planning/phases/03-stabilize-vehicle-coupling-and-selection-safety/03-CONTEXT.md` -
  `VehicleInstanceId` and linked-object identity safety principles.
- `.planning/phases/04-synchronize-event-ids-across-waypoint-segments/04-CONTEXT.md` -
  Existing `IsDeleted` history and atomic undo conventions.
- `.planning/phases/05-align-event-waypoint-panel-columns/05-CONTEXT.md` -
  Verify Phase is intentionally delayed until all numbered feature phases end.

### Existing deletion and identity behavior
- `WinFormsApp1/Forms/Form1.Shortcuts.cs` - Existing `G` shortcut soft-delete
  path and keyboard-routing integration point.
- `WinFormsApp1/Forms/Form1.Drawing.cs` - Selected-box deletion behavior and
  face/plate drawing metadata.
- `WinFormsApp1/Forms/Form1.Timeline.cs` - Current waypoint deletion and
  person/vehicle waypoint range handling.
- `WinFormsApp1/Logic/TrackingIdentityHelper.cs` - Established vehicle
  identity and waypoint matching helpers.

### JSON child links
- `WinFormsApp1/Forms/Form1.Json.cs` - JSON import/export paths that generate
  face and plate link records from active annotations.
- `WinFormsApp1/Logic/FaceLinkHelper.cs` - Face-to-body link creation and
  import restoration behavior.
- `WinFormsApp1/Logic/PlateLinkHelper.cs` - Plate-to-body link creation and
  import restoration behavior.
- `WinFormsApp1/Models/Json/LabelingDataModels.cs` - `face_annotation_id`,
  `plate_annotation_id`, and preserved `body_annotation_id` schema fields.
- `WinFormsApp1.Tests/Program.cs` - Lightweight regression harness to extend.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Form1.Shortcuts.cs` already routes `G` for a selected box and uses the
  shared undo infrastructure.
- `FaceLinkHelper` and `PlateLinkHelper` establish child-parent metadata using
  `LinkedPersonId` and `LinkedVehicleInstanceId`.
- `TrackingIdentityHelper` provides the vehicle-side identity/waypoint matching
  pattern that child-plate deletion must respect.

### Established Patterns
- Boxes use `IsDeleted` to preserve undo/history while active UI and JSON
  export paths filter deleted annotations.
- JSON link records are generated from current active face/plate annotations;
  a missing child annotation must never leave a persisted link behind.
- Waypoints define a frame-bounded segment; child deletion must be constrained
  by that range rather than by only a display label or frame coincidence.

### Integration Points
- Keyboard handling in `Form1.Shortcuts.cs` distinguishes `G` from the
  standard `Delete` flow.
- Selected-box handling in `Form1.Drawing.cs` and waypoint deletion in
  `Form1.Timeline.cs` need a common subordinate-box scope resolver.
- `Form1.Json.cs` exports `FaceLinks` and `PlateLinks`, making it the final
  guard against stale child IDs in saved JSON.

</code_context>

<specifics>
## Specific Ideas

- The user expects `event_id`-style waypoint-wide consistency for child
  deletion scope: a `Delete` operation follows the current parent waypoint,
  but `G` stays local to one frame.
- The body annotation is explicitly protected. JSON cleanup concerns only the
  deleted face/plate annotation ID and its link record.

</specifics>

<deferred>
## Deferred Ideas

None - discussion stayed within phase scope.

</deferred>

---

*Phase: 08-delete-face-and-plate-annotations-by-frame-or-waypoint*
*Context gathered: 2026-07-22*
