# Phase 4: Synchronize Event IDs Across Waypoint Segments - Context

**Gathered:** 2026-07-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Treat `EventId` as a property of an Event Waypoint. Changing the event type
from one active event box updates every active box in that same waypoint,
regardless of per-frame rectangle changes, without changing the waypoint's
segment identity.

</domain>

<decisions>
## Implementation Decisions

### Waypoint-wide event type
- **D-01:** Changing an event type in any frame updates every non-deleted event
  box in the same Event Waypoint.
- **D-02:** Rectangle position and size do not limit the update scope; a manual
  tracking adjustment must not split the event type across a waypoint.
- **D-03:** `EventInstanceId` remains unchanged. It is the primary scope key so
  another event segment with the same prior `EventId` is never changed.

### Legacy data and history
- **D-04:** When imported legacy data lacks `EventInstanceId`, resolve the same
  waypoint using its frame range and prior `EventId`, then update its active
  event boxes together.
- **D-05:** Deleted boxes remain historical records and are excluded from an
  event type update.

### Undo and feedback
- **D-06:** A waypoint-wide event type change is one atomic undo action; one
  Undo restores the prior `EventId` for every changed active box.
- **D-07:** Refresh the Event Waypoint list and current-frame Event panel
  immediately after a successful change.

### the agent's Discretion
- Choose the smallest reusable helper and test seams that fit the existing
  lightweight test harness.
- Decide the exact fallback implementation when legacy boxes are ambiguous,
  provided it cannot modify a different waypoint.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone scope
- `.planning/ROADMAP.md` - Phase 4 goal, dependency, and success criteria.
- `.planning/REQUIREMENTS.md` - `EVT-06` requirement and phase traceability.
- `.planning/STATE.md` - current milestone position and existing-worktree
  preservation constraint.

### Existing event behavior
- `WinFormsApp1/Forms/Form1.Drawing.cs` - current Event panel selection handler
  updates boxes by `EventId` and exact rectangle equality.
- `WinFormsApp1/Forms/Form1.EventPropagation.cs` - event propagation and
  manual-tracking-first guards that must remain compatible with the new scope.
- `WinFormsApp1/Logic/TrackingIdentityHelper.cs` - established event
  `EventInstanceId` identity-key and waypoint matching behavior.
- `WinFormsApp1.Tests/Program.cs` - lightweight regression harness to extend.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `TrackingIdentityHelper.GetIdentityKey(...)` and `MatchesWaypoint(...)`:
  already prefer `EventInstanceId` for event identity when it is available.
- `CloneBoundingBox(...)` and `AddUndoAction(...)`: existing undo infrastructure
  for preserving pre-change box state.

### Established Patterns
- Event boxes use `IsDeleted` as a soft-delete marker and callers filter it from
  active UI behavior.
- Waypoint membership is represented by `waypointMarkers` frame bounds plus an
  optional `EventInstanceId`.

### Integration Points
- The Event panel's `comboBox.SelectedIndexChanged` handler in
  `Form1.Drawing.cs` is the event-type mutation entry point.
- The Waypoint list refresh path is `UpdateWaypointListView()`; current-frame
  panel refresh is `UpdateEventListDisplay()` / `UpdateBboxListDisplay()`.

</code_context>

<specifics>
## Specific Ideas

- A user changing an event type at one frame expects that type to represent the
  whole Event Waypoint, not only matching rectangles or the selected frame.

</specifics>

<deferred>
## Deferred Ideas

- Event Waypoint `Entry | Exit | 객체` column alignment belongs to Phase 5.
- The PPT regression and UAT pass belongs to the numberless Verify Phase.

</deferred>

---

*Phase: 04-synchronize-event-ids-across-waypoint-segments*
*Context gathered: 2026-07-22*
