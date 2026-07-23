# Phase 8: Delete Face And Plate Annotations By Frame Or Waypoint - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md; this log preserves the alternatives considered.

**Date:** 2026-07-22
**Phase:** 08-delete-face-and-plate-annotations-by-frame-or-waypoint
**Areas discussed:** Shortcut scope, waypoint-wide child deletion, JSON link integrity

---

## Shortcut Scope

| Option | Description | Selected |
|---|---|---|
| Face/plate only | Apply the new `G` and `Delete` rules only when a subordinate face or plate box is selected; preserve body deletion. | Yes |
| All box types | Apply the new rules to body person and vehicle boxes too. | |

**User's choice:** All recommended decisions (`모두`).
**Notes:** `G` must delete only the selected face/plate box in the current frame.

---

## Waypoint-Wide Child Deletion

| Option | Description | Selected |
|---|---|---|
| Parent waypoint plus linked identity | On `Delete`, remove matching face/plate boxes only within the parent person/vehicle waypoint range and linked identity. | Yes |
| Same-frame or label-only match | Remove boxes based only on visible frame or broad label matching. | |

**User's choice:** All recommended decisions (`모두`).
**Notes:** Parent body boxes and their waypoints remain unchanged.

---

## JSON Link Integrity

| Option | Description | Selected |
|---|---|---|
| Remove child annotation and link record | Omit the deleted face/plate annotation and delete its `FaceLinks`/`PlateLinks` record while retaining the body annotation. | Yes |
| Preserve stale link | Keep a link record after deleting its child annotation. | |

**User's choice:** All recommended decisions (`모두`).
**Notes:** `body_annotation_id` must not be deleted.

---

## the agent's Discretion

- The helper layout, undo implementation details, and regression-test seams,
  provided the locked deletion and JSON outcomes remain true.

## Deferred Ideas

None.
