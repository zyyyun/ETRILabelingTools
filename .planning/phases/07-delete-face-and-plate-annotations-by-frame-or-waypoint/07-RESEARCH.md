# Phase 8 Research: Face And Plate Deletion Scope

## Current State

- `Form1.Shortcuts.cs` routes both `G` and `Delete` through selected-box
  soft-deletion. Both paths call `RecordDisappearanceIntent`, which resolves a
  waypoint and records a disappearance range; that is incompatible with the
  required current-frame-only behavior for `G` on a face or plate.
- `Delete` currently soft-deletes only the selected box when a box is selected.
  If no box is selected, it delegates to full waypoint deletion, which removes
  the waypoint and all matching parent boxes. Neither behavior implements
  child-only waypoint deletion.
- `TrackingIdentityHelper` already classifies `face` and `plate`, and its
  `GetNumericIdentity`/`MatchesWaypoint` methods resolve faces through
  `LinkedPersonId` and plates through `LinkedVehicleInstanceId`.
- JSON export groups only `!IsDeleted` boxes before creating annotations, then
  creates `FaceLinks`/`PlateLinks` only from those active frame annotations.
  Soft-deleting child boxes therefore already removes their annotations and
  link records without deleting the parent body annotation.

## Recommended Approach

1. Extract a small non-UI `SubAnnotationDeletionHelper` that recognizes
   face/plate boxes, finds the containing parent waypoint by linked identity
   and inclusive frame range, and returns only child boxes of the selected
   subtype in that waypoint.
2. Add harness coverage for face and plate scopes before wiring shortcuts:
   current-frame `G` targets one child; `Delete` targets all matching children
   in the parent waypoint; a different child identity and all parent bodies
   remain untouched.
3. Branch in `Form1.Shortcuts.cs` before the existing generic delete behavior.
   For a selected face/plate, soft-delete the helper's returned boxes and add
   undo history, but do not call `RecordDisappearanceIntent`. Keep body-box and
   no-selection behavior on their existing paths.
4. Preserve JSON export code unless testing exposes a missing `IsDeleted`
   filter. Its existing active-frame grouping is the final integrity guard for
   child annotation IDs and `face_links`/`plate_links` records.

## Risks And Guards

- A person face shares the parent `person` label, and a plate shares the parent
  `vehicle` label. Always require both child subtype and linked identity;
  broad label-based deletion would remove parent boxes.
- Multiple waypoints can reuse labels and may overlap in time. The selected
  child frame must be inside the resolved parent waypoint's inclusive range.
- `RecordDisappearanceIntent` is intentionally retained for existing generic
  body deletion but bypassed for the Phase 8 child paths so one-frame `G` does
  not suppress later child boxes.

## Validation

- The harness proves scope for face and plate deletion, including different
  identities and parent-body preservation.
- The harness verifies deleted child boxes are excluded before annotation/link
  construction, while the linked body annotation remains available.
- Build the x64 Debug solution and run the lightweight test harness.
