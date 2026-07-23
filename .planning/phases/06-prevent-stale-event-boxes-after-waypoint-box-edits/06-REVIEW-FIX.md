---
phase: 06
fixed_at: 2026-07-23T05:11:31.7115140Z
review_path: .planning/phases/06-prevent-stale-event-boxes-after-waypoint-box-edits/06-REVIEW.md
iteration: 1
findings_in_scope: 6
fixed: 6
skipped: 0
status: all_fixed
---

# Phase 06: Code Review Fix Report

**Fixed at:** 2026-07-23T05:11:31.7115140Z
**Source review:** `.planning/phases/06-prevent-stale-event-boxes-after-waypoint-box-edits/06-REVIEW.md`
**Iteration:** 1

**Summary:**

- Findings in scope: 6
- Fixed: 6
- Skipped: 0

## Fixed Issues

### CR-01: Click-only selection triggered propagation

**Files modified:** `WinFormsApp1/Forms/Form1.Drawing.cs`, `WinFormsApp1/Logic/EventRectanglePropagationUndoHelper.cs`, `WinFormsApp1.Tests/Program.cs`
**Commit:** b17454c
**Applied fix:** Gate manual-frame provenance, propagation, and history creation on an actual rectangle change.

### CR-02: Propagation Undo/Redo omitted manual-frame provenance

**Files modified:** `WinFormsApp1/Logic/EventRectanglePropagationUndoHelper.cs`, `WinFormsApp1/Forms/Form1.Undo.cs`, `WinFormsApp1/Forms/Form1.EventPropagation.cs`, `WinFormsApp1.Tests/Program.cs`
**Commit:** b17454c
**Applied fix:** Store source manual-frame provenance in each batch and restore it atomically with rectangle undo/redo.

### CR-03: Source edits with no derived targets were not undoable

**Files modified:** `WinFormsApp1/Forms/Form1.EventPropagation.cs`, `WinFormsApp1/Logic/EventRectanglePropagationUndoHelper.cs`, `WinFormsApp1.Tests/Program.cs`
**Commit:** b17454c
**Applied fix:** Always create a source batch for a real event edit and refresh event surfaces, even when the validated plan has no targets.

### CR-04: Event delete Undo/Redo used a clone instead of the tombstone

**Files modified:** `WinFormsApp1/Forms/Form1.Drawing.cs`, `WinFormsApp1/Forms/Form1.Undo.cs`, `WinFormsApp1/Logic/EventTombstoneUndoHelper.cs`, `WinFormsApp1.Tests/Program.cs`
**Commit:** b17454c
**Applied fix:** Keep the original event box reference and toggle its tombstone state through Undo/Redo.

### WR-01: Event UI was stale after delete Undo/Redo

**Files modified:** `WinFormsApp1/Forms/Form1.Undo.cs`
**Commit:** b17454c
**Applied fix:** Refresh event surfaces after tombstone Undo and Redo.

### WR-02: Unreachable legacy propagation code remained

**Files modified:** `WinFormsApp1/Forms/Form1.EventPropagation.cs`
**Commit:** b17454c
**Applied fix:** Removed the obsolete EventId-based propagation block after the planner path.

---

_Fixed: 2026-07-23T05:11:31.7115140Z_
_Fixer: the agent (gsd-code-fixer)_
_Iteration: 1_
