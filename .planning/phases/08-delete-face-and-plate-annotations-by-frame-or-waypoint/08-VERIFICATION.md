---
phase: 08-delete-face-and-plate-annotations-by-frame-or-waypoint
verified: 2026-07-23T10:30:28+09:00
status: human_needed
score: 6/7 must-haves verified automatically; UI shortcut retest deferred
---

# Phase 8: Delete Face And Plate Annotations By Frame Or Waypoint Verification Report

## Observable Truths

| # | Truth | Status | Evidence |
|---|---|---|---|
| 1 | `G` on a face or plate affects only the selected current-frame child box. | VERIFIED | `Form1.Shortcuts.cs` branches before generic deletion, sets only `selectedBox.IsDeleted`, and bypasses `RecordDisappearanceIntent`. |
| 2 | `G` leaves the same face/plate active on other frames. | VERIFIED | Child `G` does not resolve a waypoint or iterate other boxes; its code path mutates only the selected reference. |
| 3 | `Delete` scopes face deletion by parent person identity and waypoint range. | VERIFIED | Harness test `face deletion stays within linked parent waypoint` passes. |
| 4 | `Delete` scopes plate deletion by parent vehicle identity and waypoint range. | VERIFIED | Harness test `plate deletion stays within linked parent waypoint` passes. |
| 5 | Ambiguous parent waypoint resolution cannot broaden deletion. | VERIFIED | Harness test `sub annotation deletion rejects ambiguous parent waypoints` passes; helper returns an empty scope. |
| 6 | Parent body boxes and waypoint markers remain intact. | VERIFIED | Helper returns only same-subtype child boxes; shortcut soft-deletes returned boxes without removing `waypointMarkers`. |
| 7 | Running WinForms shortcuts visibly refresh and Undo restores each deleted child box. | NEEDS HUMAN | Code uses existing clone-based undo and refresh calls, but the private Form state and keyboard interaction require a running UI check. |

## JSON Integrity

- JSON export groups only active (`!IsDeleted`) boxes before annotations and child links are generated in `Form1.Json.cs`.
- Harness test `deleted child annotations cannot create export links` proves a deleted face has no exported child annotation/link input while its active body remains exportable.
- The implementation does not modify the body box, `body_annotation_id`, or waypoint marker.

## Validation

- Isolated Debug build/test output: passed. The active application locked the default `bin` DLL, so verification used `BaseOutputPath` under the local temporary directory rather than stopping the user's application.
- Test harness: passed, 56/56.
- Static shortcut check: passed; both `G` and `Delete` child branches use `SubAnnotationDeletionHelper.IsSubAnnotation(selectedBox)`.
- Diff whitespace check: passed.

## Human Verification Deferred

Milestone-wide UAT is intentionally deferred to the numberless Verify Phase.

1. Select a face at one frame, press `G`, then navigate to adjacent frames.
   Expected: only that frame's face disappears; later/earlier faces and the person body remain.
2. Select a plate or face within a parent waypoint and press `Delete`.
   Expected: matching same-subtype child boxes disappear throughout that waypoint; another object's child, the body box, and the waypoint row remain.
3. Press Undo after each deletion flow, then export JSON.
   Expected: Undo restores the intended child boxes; exported JSON has no deleted child annotation/link and retains each active body annotation.

---
*Verified: 2026-07-23T10:30:28+09:00*
*Verifier: Codex (inline verification)*
