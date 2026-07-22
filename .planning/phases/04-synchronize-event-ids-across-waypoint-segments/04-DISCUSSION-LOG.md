# Phase 4: Synchronize Event IDs Across Waypoint Segments - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md; this log preserves the alternatives considered.

**Date:** 2026-07-22
**Phase:** 04-synchronize-event-ids-across-waypoint-segments
**Areas discussed:** waypoint scope, deleted-box history, legacy data, undo behavior

---

## Waypoint Scope

| Option | Description | Selected |
|--------|-------------|----------|
| Segment-scoped | Keep `EventInstanceId`; update the entire Event Waypoint. | Yes |
| New segment | Split the event into a new segment when its type changes. | |

**User's choice:** `EventId` is a Waypoint property. Changing it in one frame
changes the whole Waypoint while retaining the segment identity.

---

## Deleted Boxes

| Option | Description | Selected |
|--------|-------------|----------|
| Active only | Change visible, non-deleted boxes and preserve deleted history. | Yes |
| Include deleted | Change soft-deleted boxes too. | |

**User's choice:** Active boxes only.

---

## Legacy Imported Data

| Option | Description | Selected |
|--------|-------------|----------|
| Waypoint fallback | Use waypoint range and prior `EventId` when `EventInstanceId` is absent. | Yes |
| Current frame only | Do not propagate without an instance ID. | |

**User's choice:** Use the waypoint fallback.

---

## Undo Behavior

| Option | Description | Selected |
|--------|-------------|----------|
| Atomic undo | One Undo restores all changed boxes in the waypoint. | Yes |
| Frame-only undo | Undo only the selected frame. | |
| No undo | Exclude undo from the Phase. | |

**User's choice:** Atomic undo.

## the agent's Discretion

- Select the smallest safe helper and test seams.

## Deferred Ideas

- Event Waypoint columns are planned separately in Phase 5.
- Full PPT regression remains in the numberless Verify Phase.
