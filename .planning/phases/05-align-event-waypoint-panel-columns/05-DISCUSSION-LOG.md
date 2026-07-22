# Phase 5: Align Event Waypoint Panel Columns - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md; this log preserves the alternatives considered.

**Date:** 2026-07-22
**Phase:** 05-align-event-waypoint-panel-columns
**Areas discussed:** Object column content, Time formatting, Column sizing and overflow

---

## Object Column Content

| Option | Description | Selected |
|--------|-------------|----------|
| Event name | Show the event type in `객체`. | Yes |
| Interacting object | Show the linked person/vehicle metadata. | |
| Both | Combine event and linked-object text. | |

**User's choice:** Show the event name only.
**Notes:** Hide interacting-object data from the list and preserve it only for JSON compatibility.

---

## Time Formatting

| Option | Description | Selected |
|--------|-------------|----------|
| Video playback time | Use the same time source as Person and Vehicle lists. | Yes |
| JSON timestamp | Prefer imported frame timestamps. | |
| Both | Show both time values. | |

**User's choice:** Always use video playback time.

---

## Column Sizing And Overflow

| Option | Description | Selected |
|--------|-------------|----------|
| Match Person/Vehicle | Reuse their widths, scrolling, and overflow behavior. | Yes |
| Event-specific layout | Tune widths separately for events. | |

**User's choice:** Make it exactly like Person and Vehicle lists.

---

## the agent's Discretion

- Safely remove or disable the invisible inline interacting-object editor while preserving JSON compatibility.

## Deferred Ideas

- Schema-level removal of `interacting_object` is outside Phase 5.
