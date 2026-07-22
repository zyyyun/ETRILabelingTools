# Phase 3: Stabilize Vehicle Coupling And Selection Safety - Context

**Gathered:** 2026-07-21
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase stabilizes vehicle identity and list-selection safety around event workflows. It fixes vehicle duplication and split identities triggered during event-assisted operations, and it makes waypoint deletion respect the currently active list selection. It preserves the earlier phase decisions that events are finalized independently and that event lifetime is clamped to its own waypoint range.

</domain>

<decisions>
## Implementation Decisions

### Vehicle identity model
- **D-01:** `event` remains behaviorally independent from `vehicle`.
- **D-02:** Vehicle internal identity is anchored to `VehicleInstanceId` as the single source of truth for one logical vehicle.
- **D-03:** Event workflows must not cause one logical vehicle to split into multiple apparent identities such as `vehicle_car_01` and `vehicle_car_02`.

### Event-assisted vehicle behavior
- **D-04:** During event work, the tool may attach to an already matched vehicle body, but it must not auto-create a new vehicle bbox.
- **D-05:** If a vehicle already exists for the current frame/range, event behavior reuses that matched vehicle body instead of generating a duplicate helper vehicle.
- **D-06:** Vehicle duplication should be prevented at creation time, not cleaned up later by dedupe as a primary strategy.

### Selection ownership
- **D-07:** The currently clicked waypoint list is the only active selection owner.
- **D-08:** Stale selection from another list must be ignored or cleared before delete/edit actions run.
- **D-09:** Clicking empty space in a waypoint list clears that list selection so users can safely change targets.

### the agent's Discretion
- The exact helper layout for active-list ownership and selection clearing.
- Whether selection ownership is enforced in list click handlers, delete handlers, or both, as long as stale cross-list selection cannot delete the wrong waypoint.

</decisions>

<specifics>
## Specific Ideas

- The user clarified that `event` should move independently from `vehicle`; the phase exists to remove the accidental coupling side effects, not to formalize a dependency.
- The PPT complaints to satisfy here are the vehicle duplication/split cases and the wrong-target deletion caused by lingering person selection.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone scope
- `.planning/PROJECT.md` - Milestone goals and out-of-scope boundaries.
- `.planning/REQUIREMENTS.md` - Phase-mapped requirements `TRK-01`, `TRK-02`, `UI-01`, `UI-02`, and `UI-03`.
- `.planning/ROADMAP.md` - Phase 3 goal and success criteria.
- `.planning/STATE.md` - Current handoff state after Phase 2.

### Prior phase decisions
- `.planning/phases/01-normalize-event-creation-and-exit-finalization/01-CONTEXT.md` - Event creation/finalization independence rules.
- `.planning/phases/02-clamp-event-lifetime-and-cleanup-derived-artifacts/02-CONTEXT.md` - Event lifetime and manual-tracking-first cleanup rules that Phase 3 must preserve.
- `.planning/phases/02-clamp-event-lifetime-and-cleanup-derived-artifacts/02-01-SUMMARY.md` - Instance-safe trim and termination results.
- `.planning/phases/02-clamp-event-lifetime-and-cleanup-derived-artifacts/02-02-SUMMARY.md` - Manual-tracking-first helper constraints and instance-safe deletion results.

### Event stability design
- `docs/superpowers/specs/2026-07-21-event-waypoint-stabilization-design.md` - Milestone-wide design and original problem grouping.

### Relevant code
- `WinFormsApp1/Logic/TrackingIdentityHelper.cs` - Vehicle identity comparison and waypoint matching helpers.
- `WinFormsApp1/Forms/Form1.EntryExit.cs` - Event-assisted entry/exit flows that currently touch vehicle waypoint creation/merge behavior.
- `WinFormsApp1/Forms/Form1.Timeline.cs` - Waypoint selection and delete handling across person/vehicle/event lists.
- `WinFormsApp1/Forms/Form1.Drawing.cs` - Box selection, sidebar highlighting, and draw/select mode transitions.
- `WinFormsApp1.Tests/Program.cs` - Lightweight regression hooks for identity and waypoint behavior.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `TrackingIdentityHelper.GetNumericIdentity(...)` and `TrackingIdentityHelper.MatchesWaypoint(...)` already provide the right direction for `VehicleInstanceId`-based matching.
- Existing tests already cover parts of vehicle waypoint matching and linked plate behavior in `WinFormsApp1.Tests/Program.cs`.
- List views for person, vehicle, and event are already separated in `Form1.Timeline.cs`, so ownership enforcement can likely be localized to list handlers plus delete routing.

### Established Patterns
- Vehicle selection and deletion logic still mixes older `VehicleId` assumptions with newer `TrackingIdentityHelper` paths.
- Event workflows currently pass through code that can still create or merge vehicle waypoint artifacts when overlap is present.
- The current delete path checks person, then vehicle, then event selected items in sequence, which is exactly the stale-selection hazard the user reported.

### Integration Points
- Vehicle identity stabilization likely centers on `TrackingIdentityHelper` plus entry/exit event-assisted logic.
- Selection ownership needs coordinated updates across list click handlers, timeline selection sync, and delete button handling.
- Regression hooks should include one same-vehicle event overlap case and one cross-list stale selection deletion case.

</code_context>

<deferred>
## Deferred Ideas

- Full replay and certification-style documentation of the PPT scenarios remains Phase 4 work.
- Any broad UI redesign of the waypoint lists remains out of scope unless required for minimal selection ownership enforcement.
- New vehicle labeling capabilities are out of scope; this phase only stabilizes existing behavior.

</deferred>

---

*Phase: 03-stabilize-vehicle-coupling-and-selection-safety*
*Context gathered: 2026-07-21*
