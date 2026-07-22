---
status: partial
phase: 04-synchronize-event-ids-across-waypoint-segments
source: [04-VERIFICATION.md]
started: 2026-07-22T08:46:00+09:00
updated: 2026-07-22T09:10:00+09:00
---

## Current Test

[QA PPT 3 result recorded; gap-closure plan required before retest]

## Tests

### 1. Waypoint-wide event type refresh
expected: Changing one active event box to another type updates every active
box in that Event Waypoint despite different rectangles, and immediately
refreshes the Event Waypoint list and current-frame Event panel.
result: failed
actual: QA PPT 3 reports that changing the event name changed only the selected
frame; the Event Waypoint name and the remaining boxes did not update.

### 2. Atomic event type undo and redo
expected: One Undo restores the prior EventId for every changed active box;
one Redo reapplies the new EventId for those same boxes.
result: [pending]

## Summary

total: 2
passed: 0
issues: 1
pending: 1
skipped: 0
blocked: 0

## Gaps

`EVT-06` failed QA PPT 3. See `04-VERIFICATION.md` and
`.planning/verification/event-waypoint-regression/04-PPT-VERIFICATION.md`.
