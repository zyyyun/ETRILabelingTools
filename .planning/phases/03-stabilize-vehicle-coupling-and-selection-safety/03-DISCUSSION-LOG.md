# Phase 3: Stabilize Vehicle Coupling And Selection Safety - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions captured in CONTEXT.md; this log preserves the discussion.

**Date:** 2026-07-21
**Phase:** 03-stabilize-vehicle-coupling-and-selection-safety
**Mode:** discuss
**Areas discussed:** Vehicle identity model, Event-assisted vehicle behavior, Selection ownership

## Discussion Summary

### Vehicle identity model
- User clarification:
  - `event` should remain independent from `vehicle`
  - the real issue is accidental vehicle split/duplication during event work
- Decision:
  - use `VehicleInstanceId` as the single internal identity for one logical vehicle

### Event-assisted vehicle behavior
- Options considered:
  - reuse only an already matched vehicle body
  - allow vehicle auto-generation and clean it up later
  - always ask the user before vehicle-related event handling
- User selected:
  - event work may connect to an existing matched vehicle body
  - event work must not auto-create a new vehicle bbox

### Selection ownership
- Options considered:
  - the currently clicked list owns active selection
  - keep multi-list selection and only tweak delete priority
  - keep stale selection and rely on extra confirmation
- User selected:
  - current list ownership is authoritative
  - stale selection from other lists must be ignored or cleared
  - empty-space click should clear selection
