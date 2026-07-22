# Phase 2: Clamp Event Lifetime And Cleanup Derived Artifacts - Context

**Gathered:** 2026-07-21
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase clamps event lifetime to the finalized waypoint range and makes cleanup instance-safe. It defines when event boxes must disappear after exit, what artifacts are removed together during event termination or deletion, and how propagation or retracking must respect the event exit boundary. It does not change the Phase 1 rule that only `Exit` / `X` finalizes event creation, and it does not yet tackle vehicle identity repair or broad selection-state fixes.

</domain>

<decisions>
## Implementation Decisions

### Event lifetime boundary
- **D-01:** An event box is valid only through its finalized `ExitFrame`.
- **D-02:** Starting from the frame immediately after `ExitFrame`, the event box must be absent from UI rendering, in-memory box collections, and saved JSON output.

### Cleanup scope
- **D-03:** Event cleanup operates on `EventInstanceId`, not broad `EventId` groups.
- **D-04:** Terminating or deleting an event removes the event boxes and event waypoint records that belong to that same `EventInstanceId`.
- **D-05:** Other event instances of the same event type must remain untouched.

### Propagation and retracking limits
- **D-06:** All event-related propagation, interpolation, and retracking use the finalized waypoint `ExitFrame` as a hard cap.
- **D-07:** New event boxes beyond `ExitFrame` must not be created.
- **D-08:** If stale boxes already exist beyond `ExitFrame`, the cleanup path trims them back to the valid range.
- **D-09:** Following the `AOLTv1.0` event waypoint model, event waypoint segments are manual-tracking-first. Automatic event propagation or retracking is disabled by default after event creation.
- **D-10:** The only automatic behaviors allowed for event segments in this phase are exit-bound trimming and cleanup that enforce the finalized range.

### the agent's Discretion
- The exact helper layout for instance-scoped cleanup and clipping.
- Whether cleanup is centralized in a new helper or routed through existing `TerminateEventFromCurrentFrame`, propagation helpers, and delete handlers, as long as the behavior matches the locked rules above.
- How the UI communicates that event segments are now manual-tracking-first, as long as it does not expand scope into a broader UI redesign.

</decisions>

<specifics>
## Specific Ideas

- Phase 1 already locked `Entry temporary + Exit/X finalizes`; Phase 2 must preserve that rule and only manage post-finalization lifetime.
- The user expectation is strict: if an event is over, the box should be gone everywhere, not just hidden until save.
- The July 20, 2026 PPT explicitly calls out the `ASLT GS` certification tool workflow: event waypoint ranges should be handled by manual tracking, and mistaken exit-frame box drawing or auto tracking should not be the normal path.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone scope
- `.planning/PROJECT.md` - Current milestone goal and out-of-scope boundaries.
- `.planning/REQUIREMENTS.md` - Phase-mapped requirements `EVT-04`, `EVT-05`, and `TRK-03`.
- `.planning/ROADMAP.md` - Phase 2 goal and success criteria.
- `.planning/STATE.md` - Current phase completion status and handoff state from Phase 1.

### Prior phase decisions
- `.planning/phases/01-normalize-event-creation-and-exit-finalization/01-CONTEXT.md` - Locked creation/finalization rules that Phase 2 must preserve.
- `.planning/phases/01-normalize-event-creation-and-exit-finalization/01-01-SUMMARY.md` - Summary of pending-event instance reuse changes.
- `.planning/phases/01-normalize-event-creation-and-exit-finalization/01-02-SUMMARY.md` - Summary of eager-helper guard changes and regression hook.

### Event stability design
- `docs/superpowers/specs/2026-07-21-event-waypoint-stabilization-design.md` - Milestone-wide design and phase boundaries.
- `docs/superpowers/specs/2026-06-29-event-persistence-design.md` - Event identity and event-instance stability model to preserve during cleanup.
- `C:/Users/ANNA/AOLTv1.0/Forms/MainForm.cs` - Reference implementation for event waypoint exit shortening, range clamping, and manual-tracking-first event handling.

### Relevant code
- `WinFormsApp1/Forms/Form1.EventPropagation.cs` - Event propagation, helper cleanup, and event-range extension logic.
- `WinFormsApp1/Forms/Form1.EntryExit.cs` - Finalized event range construction and exit semantics.
- `WinFormsApp1/Forms/Form1.Json.cs` - `Q`-based termination path and event save/load behavior.
- `WinFormsApp1/Forms/Form1.Inertia.cs` - Event retracking, interpolation, and range deletion behavior.
- `WinFormsApp1/Forms/Form1.Timeline.cs` - Waypoint deletion behavior that currently removes event boxes by range.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `EventFinalizationHelper.NormalizePendingEventInstanceIds(...)` already gives Phase 2 an instance-stable event range before cleanup begins.
- `FindWaypointForBox(...)` already prefers `EventInstanceId` when matching event boxes to waypoints.
- `WaypointOverlapHelper` and existing range-based deletion helpers can likely be reused after being narrowed to instance scope.

### Established Patterns
- `TerminateEventFromCurrentFrame()` in `Form1.Json.cs` already updates `ExitFrame`, so it is a natural place to enforce hard range trimming.
- `PropagateEventBoxWithinRange(...)`, `PropagateEventBoxFromCurrentFrame(...)`, and inertia-driven helpers currently extend or mutate event boxes based on waypoint range, but not all paths are guaranteed to clean stale overflow.
- `btnDeleteSelectedWaypoint_Click()` in `Form1.Timeline.cs` still deletes event boxes by `EventId` + frame range, which is too broad for the locked Phase 2 cleanup rule.
- In `AOLTv1.0`, exit shortening removes boxes beyond the new exit immediately and event propagation is treated as bounded helper behavior, not an invitation for free-form auto extension after mistakes.

### Integration Points
- Event cleanup must touch both the propagation helpers and explicit delete/terminate entry points.
- Save-time JSON output should act as a consistency backstop, but not the only place where overflow event boxes disappear.
- Any path that shortens an event range must also trigger instance-scoped pruning so stale post-exit boxes do not survive in memory.
- Event helper entry points that still imply automatic extension should either be disabled for event segments or reduced to bounded range-fill logic only.

</code_context>

<deferred>
## Deferred Ideas

- Vehicle identity and duplicate vehicle box repairs remain Phase 3 work.
- Broad list selection and wrong-target deletion UX remain Phase 3 work, except where a small change is strictly required to honor instance-scoped event cleanup.
- Full PPT replay and documentation belongs to Phase 4, although Phase 2 should leave good hooks for that verification.

</deferred>

---

*Phase: 02-clamp-event-lifetime-and-cleanup-derived-artifacts*
*Context gathered: 2026-07-21*
