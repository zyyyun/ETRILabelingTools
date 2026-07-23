# Phase 6: Prevent stale event boxes after waypoint box edits - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md; this log preserves the alternatives considered.

**Date:** 2026-07-23
**Phase:** 06-prevent-stale-event-boxes-after-waypoint-box-edits
**Areas discussed:** propagation range, manual-edit precedence, deletion semantics, immediate feedback

---

## Propagation range

| Option | Description | Selected |
|--------|-------------|----------|
| Current frame only | Edit only the box on the current frame. | |
| Current frame to waypoint exit | Update forward through the active waypoint's exit frame. | Yes |
| Entire waypoint | Rewrite both earlier and later frames in the waypoint. | |

**User's choice:** Current frame to waypoint exit.
**Notes:** Earlier frames must remain unchanged.

---

## Manual-edit precedence

| Option | Description | Selected |
|--------|-------------|----------|
| Preserve later manual corrections | Do not overwrite frames manually corrected after the edit frame. | Yes |
| Rewrite all later frames | Apply the new edit through all later frames regardless of manual corrections. | |

**User's choice:** Preserve later manual corrections.
**Notes:** Automatic propagation updates only frames still in the derived state.

---

## Deletion semantics

| Option | Description | Selected |
|--------|-------------|----------|
| Delete later boxes too | A frame deletion removes following propagated boxes. | |
| Preserve later boxes | A frame deletion affects only that frame; full deletion requires waypoint deletion. | Yes |

**User's choice:** Preserve later boxes.
**Notes:** The user explicitly distinguished single-frame deletion from waypoint deletion.

---

## Immediate feedback

| Option | Description | Selected |
|--------|-------------|----------|
| Refresh immediately | Update current display and Event Waypoint list after the change. | Yes |
| Refresh on navigation | Let a later navigation redraw the UI. | |

**User's choice:** Refresh immediately.
**Notes:** The result must be visible without changing frames.

---

## the agent's Discretion

- Implementation representation for protected manual corrections.
- Exact regression test shape and UI refresh ordering.

## Deferred Ideas

- Tracked-object changes belong to Phase 7.
