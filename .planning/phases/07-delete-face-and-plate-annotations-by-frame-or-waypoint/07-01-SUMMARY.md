---
phase: 08-delete-face-and-plate-annotations-by-frame-or-waypoint
plan: 01
subsystem: annotation deletion
tags: [winforms, bounding-box, waypoint, face, plate, json]
requires:
  - phase: 03-stabilize-vehicle-coupling-and-selection-safety
    provides: Linked vehicle identity and waypoint matching rules
provides:
  - Frame-local face/plate deletion with G
  - Waypoint-scoped face/plate deletion with Delete
  - Child annotation/link export regression coverage
affects: [verify-phase, face-labeling, plate-labeling, json-export]
tech-stack:
  added: []
  patterns: [fail-closed subordinate annotation scope resolution]
key-files:
  created:
    - WinFormsApp1/Logic/SubAnnotationDeletionHelper.cs
  modified:
    - WinFormsApp1/Forms/Form1.Shortcuts.cs
    - WinFormsApp1.Tests/Program.cs
key-decisions:
  - "Child deletion requires subtype, linked identity, and one containing parent waypoint."
  - "Frame-local child deletion bypasses RecordDisappearanceIntent."
patterns-established:
  - "Subordinate annotation actions must preserve their parent body and waypoint."
requirements-completed: []
duration: 30min
completed: 2026-07-23
---

# Phase 8 Plan 01: Delete Face And Plate Annotations By Frame Or Waypoint Summary

**Face and plate shortcuts now delete only the requested child annotation scope while preserving parent waypoint and JSON body-link integrity.**

## Performance

- **Duration:** 30 min
- **Started:** 2026-07-23T10:00:00+09:00
- **Completed:** 2026-07-23T10:30:28+09:00
- **Tasks:** 2
- **Files modified:** 3

## Accomplishments
- Added a fail-closed helper that resolves face/plate deletion by child subtype, linked parent identity, and one containing waypoint range.
- Made `G` soft-delete only the selected face/plate box and bypass range disappearance recording.
- Made `Delete` soft-delete matching face/plate boxes in the resolved parent waypoint without removing the body box or waypoint marker.
- Added face, plate, ambiguity, and JSON child-link eligibility coverage to the lightweight harness.

## Task Commits

1. **Task 1: Extract and test face/plate waypoint deletion scope** - `3647117` (`test`)
2. **Task 2: Route G and Delete through the child-only deletion scope** - `21b2255` (`feat`)

## Files Created/Modified
- `WinFormsApp1/Logic/SubAnnotationDeletionHelper.cs` - Resolves safe, subtype-specific child deletion sets.
- `WinFormsApp1/Forms/Form1.Shortcuts.cs` - Routes child `G` and `Delete` actions before generic box deletion.
- `WinFormsApp1.Tests/Program.cs` - Covers waypoint range, identity isolation, ambiguous scope, and child-link export eligibility.

## Decisions Made
- `G` on a face or plate stays frame-local and does not call `RecordDisappearanceIntent`, preventing later-frame child boxes from being hidden.
- `Delete` fails closed with an informational message when it cannot resolve exactly one parent waypoint.
- Existing JSON export is retained: its active-box filter omits deleted child annotations before generating `FaceLinks` or `PlateLinks`, while active body annotations remain.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- The running `WinFormsApp1.exe` locked the default Debug output DLL. Verification therefore used an isolated temporary `BaseOutputPath`; it compiled and ran the same source without stopping the user's application.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Automated scope and export eligibility coverage is complete.
- Manual UI verification is intentionally deferred to the numberless Verify Phase with the rest of the milestone, as requested.

---
*Phase: 08-delete-face-and-plate-annotations-by-frame-or-waypoint*
*Completed: 2026-07-23*
