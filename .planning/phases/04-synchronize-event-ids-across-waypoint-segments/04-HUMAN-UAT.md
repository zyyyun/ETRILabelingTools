---
status: partial
phase: 04-synchronize-event-ids-across-waypoint-segments
source: [04-VERIFICATION.md]
started: 2026-07-22T08:46:00+09:00
updated: 2026-07-22T08:46:00+09:00
---

## Current Test

[awaiting human testing]

## Tests

### 1. Waypoint-wide event type refresh
expected: Changing one active event box to another type updates every active
box in that Event Waypoint despite different rectangles, and immediately
refreshes the Event Waypoint list and current-frame Event panel.
result: [pending]

### 2. Atomic event type undo and redo
expected: One Undo restores the prior EventId for every changed active box;
one Redo reapplies the new EventId for those same boxes.
result: [pending]

## Summary

total: 2
passed: 0
issues: 0
pending: 2
skipped: 0
blocked: 0

## Gaps

None recorded. UAT has not run.
