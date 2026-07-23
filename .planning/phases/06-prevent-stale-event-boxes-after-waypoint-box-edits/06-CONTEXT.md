# Phase 6: Prevent stale event boxes after waypoint box edits - Context

**Gathered:** 2026-07-23
**Status:** Ready for planning

<domain>
## Phase Boundary

Keep Event Waypoint boxes synchronized after a user manually edits a box in a
waypoint. The change applies only to the edited frame through the waypoint's
exit frame and must preserve later manual corrections and individual deletions.
Changing the tracked object is reserved for Phase 7.

</domain>

<decisions>
## Implementation Decisions

### Propagation range
- **D-01:** A manual move or resize of an event box propagates its rectangle
  from the current frame through the containing Event Waypoint's exit frame.
- **D-02:** Frames before the edited frame remain unchanged.

### Manual-edit precedence
- **D-03:** A later frame that has been manually corrected is protected from
  automatic propagation. A new edit updates only the still automatically
  propagated portion of the waypoint.

### Deletion semantics
- **D-04:** Deleting an event box on one frame removes only that frame's box;
  later event boxes remain intact.
- **D-05:** Deleting the Event Waypoint remains the only action that removes
  event boxes across the full waypoint range.

### Immediate feedback
- **D-06:** After an edit or deletion, refresh both the current frame display
  and the Event Waypoint list immediately so the visible state reflects the
  change without navigation.

### the agent's Discretion
- Choose the smallest durable representation for distinguishing manual
  corrections from derived boxes, while preserving existing JSON compatibility.
- Reuse the established event identity and active-waypoint scope helpers;
  determine the focused regression coverage and refresh ordering.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone scope
- `.planning/ROADMAP.md` - Phase 6 placement after event ID and panel work;
  Phase 7 owns tracked-object changes.
- `.planning/PROJECT.md` - Stability over convenience is the milestone's core
  value.
- `.planning/REQUIREMENTS.md` - Existing event lifecycle and tracking
  constraints that must not regress.
- `.planning/STATE.md` - Current milestone progress and planning state.

### Event waypoint behavior
- `WinFormsApp1/Forms/Form1.Drawing.cs` - Event list refresh, current-frame
  display updates, and drag/resize edit paths.
- `WinFormsApp1/Forms/Form1.EventPropagation.cs` - Existing event propagation,
  range cleanup, and manual-tracking guards.
- `WinFormsApp1/Logic/EventWaypointUpdateHelper.cs` - Active scope resolution
  and `EventInstanceId`-based event identity utilities.
- `WinFormsApp1/Logic/TrackingIdentityHelper.cs` - Waypoint matching rules.
- `WinFormsApp1/Models/Annotation/WaypointMarker.cs` - Waypoint entry/exit
  range and event identity fields.
- `docs/superpowers/specs/2026-07-21-event-waypoint-stabilization-design.md` -
  Existing event persistence and stabilization design decisions.
- `WinFormsApp1.Tests/Program.cs` - Lightweight regression harness patterns.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `EventWaypointUpdateHelper.ResolveActiveScope`: resolves the active Event
  Waypoint using `EventInstanceId` and its frame range.
- `TrackingIdentityHelper.MatchesWaypoint`: already prefers `EventInstanceId`
  when matching boxes to event waypoints.
- Existing propagation and cleanup routines in `Form1.EventPropagation.cs`:
  provide range-aware integration points.

### Established Patterns
- Event type changes synchronize all non-deleted boxes in the active waypoint
  by `EventInstanceId`, independently of rectangle geometry.
- Event propagation is currently guarded for manual-tracking-first behavior;
  Phase 6 must change only the box-edit synchronization path without widening
  assisted tracking behavior.

### Integration Points
- `pictureBoxVideo_MouseDown`, `pictureBoxVideo_MouseMove`, and
  `pictureBoxVideo_MouseUp` in `Form1.Drawing.cs` perform manual box edits.
- `PropagateEventBoxFromCurrentFrame` and related methods in
  `Form1.EventPropagation.cs` are the primary range-update seam.
- `UpdateWaypointListView()` and `UpdateEventListDisplay()` provide the
  immediate UI refresh seams.

</code_context>

<specifics>
## Specific Ideas

- The workflow should preserve a labeler's later per-frame corrections rather
  than allowing an earlier edit to overwrite them.
- A single-frame deletion is not equivalent to deleting the Event Waypoint.

</specifics>

<deferred>
## Deferred Ideas

- Synchronizing event boxes after the tracked person or vehicle changes is
  Phase 7 and is explicitly out of scope here.
- Broader changes to event taxonomy, JSON schema, or non-event waypoint
  behavior remain out of scope.

</deferred>

---

*Phase: 06-prevent-stale-event-boxes-after-waypoint-box-edits*
*Context gathered: 2026-07-23*
