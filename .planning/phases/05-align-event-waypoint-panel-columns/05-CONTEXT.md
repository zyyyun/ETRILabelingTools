# Phase 5: Align Event Waypoint Panel Columns - Context

**Gathered:** 2026-07-22
**Status:** Ready for planning

<domain>
## Phase Boundary

Make the Event Waypoint list follow the existing Person and Vehicle waypoint
list convention: `Entry`, `Exit`, and `객체`, with compatible selection and
editing behavior. This phase changes the Event list presentation only; it does
not remove JSON fields or redesign waypoint workflows.

</domain>

<decisions>
## Implementation Decisions

### Event list columns
- **D-01:** Event Waypoints use exactly the same three visible columns as
  Person and Vehicle: `Entry`, `Exit`, `객체`, in that order.
- **D-02:** The event type/name is displayed in the `객체` column.
- **D-03:** `InteractingObject` is hidden from the Event Waypoint list. Keep
  the existing JSON field for import/export compatibility; do not remove or
  migrate stored data in this phase.

### Time and layout consistency
- **D-04:** `Entry` and `Exit` always display video playback time, using the
  same formatting and source as Person and Vehicle Waypoints. JSON timestamps
  are not displayed in the Event list.
- **D-05:** Reuse Person/Vehicle list sizing, column widths, overflow, and
  scrolling behavior rather than creating Event-specific layout rules.

### the agent's Discretion
- Preserve current Event list selection/deletion wiring while removing the
  no-longer-visible inline interacting-object edit path or making it inert.
- Choose the smallest regression coverage that verifies the three-column row
  shape and video-time source.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone scope
- `.planning/ROADMAP.md` - Phase 5 goal, `UI-04`, and dependency on Phase 4.
- `.planning/REQUIREMENTS.md` - `UI-04` acceptance intent and traceability.
- `.planning/STATE.md` - milestone status and deferred Verify Phase decision.

### Existing waypoint-list behavior
- `WinFormsApp1/Form1.Designer.cs` - Person/Vehicle list columns are
  `Entry` (80), `Exit` (80), `객체` (95); Event currently has four divergent
  columns.
- `WinFormsApp1/Forms/Form1.Drawing.cs` - `UpdateWaypointListView()` builds
  Event rows and currently substitutes JSON/subtitle timestamps.
- `WinFormsApp1/Forms/Form1.Timeline.cs` - Event list selection and inline
  `InteractingObject` editing behavior to preserve or disable safely.
- `WinFormsApp1/Forms/Form1.Json.cs` - `interacting_object` import/export
  compatibility that must remain intact even when hidden in the UI.
- `WinFormsApp1.Tests/Program.cs` - existing lightweight regression harness.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- Person and Vehicle `ListView` designer settings are the exact visual and
  behavioral template for Event columns.
- `waypoint.EntryTime` and `waypoint.ExitTime` already provide the required
  playback-time display values.

### Established Patterns
- Every waypoint list uses `View.Details`, `FullRowSelect`, a 260px list area,
  and common panel height calculation.
- `InteractingObject` is persisted on `WaypointMarker` and JSON annotations,
  independent of whether the Event list displays it.

### Integration Points
- Event column definitions live in `Form1.Designer.cs`.
- Event row contents are built in `Form1.Drawing.cs`.
- Inline subitem editing is handled in `Form1.Timeline.cs` and must not target
  an invalid column after the list is reduced to three columns.

</code_context>

<specifics>
## Specific Ideas

- The Event panel should look and behave like the Person and Vehicle panels,
  not expose a separate timestamp-oriented layout.

</specifics>

<deferred>
## Deferred Ideas

- Removing `interacting_object` from the JSON schema is out of scope; retain
  it for compatibility.
- Full milestone verification, including Phase 4 QA retest, occurs in the
  numberless Verify Phase after all feature phases finish.

</deferred>

---

*Phase: 05-align-event-waypoint-panel-columns*
*Context gathered: 2026-07-22*
