# Phase 2: Clamp Event Lifetime And Cleanup Derived Artifacts - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions captured in CONTEXT.md; this log preserves the discussion.

**Date:** 2026-07-21
**Phase:** 02-clamp-event-lifetime-and-cleanup-derived-artifacts
**Mode:** discuss
**Areas discussed:** Event lifetime boundary, Cleanup scope, Propagation and retracking limits

## Discussion Summary

### Event lifetime boundary
- Options considered:
  - Event box is valid only through `ExitFrame` and disappears immediately after
  - Keep overflow in memory and trim only on save/reload
  - Let parent object lifetime keep the event visible
- User selected:
  - Event boxes are valid only through `ExitFrame`
  - From the next frame onward they must disappear from UI, memory, and saved output

### Cleanup scope
- Options considered:
  - Cleanup by `EventInstanceId`
  - Cleanup by broad `EventId`
  - Remove boxes only and leave waypoint cleanup elsewhere
- User selected:
  - Cleanup is instance-scoped by `EventInstanceId`
  - Same-type sibling events must remain untouched

### Propagation and retracking limits
- Options considered:
  - `ExitFrame` is a hard cap and stale overflow gets trimmed
  - Allow overflow until save time
  - Only clean overflow on manual terminate/delete
- User selected:
  - `ExitFrame` is a hard cap
  - Overflow creation is blocked
  - Existing overflow boxes are trimmed during cleanup

### AOL reference behavior
- Additional reference provided:
  - `C:\Users\ANNA\AOLTv1.0` event waypoint handling should be used as the behavioral reference for the PPT note about manual tracking
- User selected:
  - follow the AOL-style manual-tracking-first model for event waypoint segments
  - disable default automatic event propagation or retracking beyond bounded cleanup behavior

## Deferred Ideas

- Vehicle-specific overlap and identity stabilization remain in Phase 3.
- Full regression walkthroughs remain in Phase 4.
