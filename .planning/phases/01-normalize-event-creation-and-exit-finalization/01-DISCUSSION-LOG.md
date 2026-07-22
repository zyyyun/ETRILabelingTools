# Phase 1: Normalize Event Creation And Exit Finalization - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions captured in CONTEXT.md; this log preserves the discussion.

**Date:** 2026-07-21
**Phase:** 01-normalize-event-creation-and-exit-finalization
**Mode:** discuss
**Areas discussed:** Event confirmation timing, Exit-frame event bbox semantics, Vehicle overlap behavior during creation

## Discussion Summary

### Event confirmation timing
- Options considered:
  - `Entry` temporary, `Exit/X` confirms one waypoint
  - `Entry` immediately creates a waypoint, `Exit` updates range
  - First event bbox creates the waypoint and `Entry/Exit` become metadata
- User selected:
  - `Entry` remains temporary
  - `Exit button/X` is the only waypoint confirmation point

### Exit-frame event bbox semantics
- Options considered:
  - Use exit-frame bbox as the last frame box only
  - Ignore exit-frame bbox and trust the original flow
  - Treat exit-frame bbox as a second event candidate
- User selected:
  - Exit-frame bbox is accepted as the final box state only
  - It must not create an additional waypoint

### Vehicle overlap behavior during creation
- Options considered:
  - Vehicle overlap is supporting context only
  - Vehicle overlap can merge or update vehicle waypoint state during event creation
  - Vehicle overlap always triggers another confirmation step
- User selected:
  - Vehicle overlap does not affect whether an event waypoint is created
  - It may inform matching only, not creation-side branching

## Deferred Ideas

- Vehicle identity stabilization and overlap-side effects beyond creation flow should be handled in Phase 3.
- Event lifetime clamping and cleanup should be handled in Phase 2.

## Notes

- The phase discussion intentionally stayed within existing scope and did not add new capabilities.
- The July 20, 2026 PPT remains the primary reproduction source for later regression verification.
