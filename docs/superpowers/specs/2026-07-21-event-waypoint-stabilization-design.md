# Event Waypoint Stabilization Design

## Goal
Stabilize Event Waypoint behavior in the ETRI labeling tool so that event creation, exit handling, auto-tracking, deletion, and overlapping vehicle scenarios behave consistently across manual and assisted labeling flows.

## Background
The July 20, 2026 PPT lists a cluster of regressions concentrated around Event Waypoint handling:

- creating two event waypoints for a single action
- keeping event boxes alive after the declared exit frame
- creating duplicate vehicle boxes when event exit is confirmed from a drawn bbox
- leaving undeletable ghost event waypoints behind
- deleting the wrong waypoint because list selection state is stale
- splitting a single vehicle object into mismatched IDs such as `vehicle_car_01` and `vehicle_car_02`
- creating unintended contact waypoints during event workflows

These problems are not isolated bugs. They point to inconsistent lifecycle rules across event creation, auto-track propagation, interpolation, list selection, and deletion paths.

## Recommended Scope
This milestone should be a stabilization milestone, not a structural rewrite.

Included:
- normalize event creation and exit confirmation rules
- stop event persistence beyond exit
- prevent duplicate event and vehicle boxes during exit-assisted workflows
- fix wrong-target deletion caused by stale selection state
- eliminate ghost event or contact waypoint artifacts
- restore consistent object identity for vehicles touched by event flows
- add regression coverage for the reproduced PPT scenarios

Excluded:
- broad UI redesign
- large-scale `Form1` decomposition
- JSON schema redesign unrelated to event stability
- new label classes or new annotation modes

## Approaches Considered

### 1. Minimal hotfix set
Patch each reproduced bug locally where it appears.

Pros:
- fastest delivery
- smallest code diff

Cons:
- high regression risk because lifecycle rules stay fragmented
- likely to miss linked paths like auto-track and interpolation

### 2. Stabilization milestone with shared event rules
Define shared lifecycle rules for event creation, exit confirmation, propagation, deletion, and selection ownership, then patch each affected path to use those rules.

Pros:
- matches the actual bug cluster
- keeps scope reasonable for the current milestone
- lowers repeat regressions without demanding a full refactor

Cons:
- slightly broader than a hotfix sprint

### 3. Full waypoint subsystem rewrite
Extract event and vehicle waypoint logic into new services or separate components before fixing bugs.

Pros:
- best long-term architecture

Cons:
- too risky and too large for the requested fix cycle

Recommendation: Approach 2.

## Design

### 1. Canonical event lifecycle
Every event waypoint must follow one shared lifecycle:

1. event entry starts a pending event segment
2. exit confirmation finalizes exactly one event waypoint
3. propagation and auto-track can extend boxes only within the finalized event range
4. deletion removes the full event segment and all derived boxes tied to it

This design removes the current split behavior where different entry and exit paths create different numbers of event objects.

### 2. Exit is the hard boundary
The exit frame is authoritative.

Rules:
- no event bbox should survive past the event waypoint exit frame
- auto-track and interpolation must clamp generated boxes to the event exit frame
- drawing an exit-frame bbox must not create a second event segment
- keyboard exit (`X`) and button exit must resolve through the same handler

### 3. Assisted tracking must not fork identities
When event creation touches a vehicle object, the tool must preserve the original object identity instead of generating a second logical vehicle.

Rules:
- event-assisted box generation cannot allocate a new vehicle identity when an existing overlapped vehicle is already selected or matched
- generated helper boxes must inherit the matched object identity
- duplicate entry-frame boxes for the same vehicle and event range must be rejected

### 4. Waypoint ownership and deletion safety
Deletion must operate on the visually selected waypoint type, not on stale prior selection.

Rules:
- selecting `person`, `vehicle`, and `event` waypoints clears incompatible prior selections
- clicking empty list space clears active selection
- delete commands resolve the current selected list owner before mutating data
- event deletion must remove orphaned derived boxes and waypoint markers together

### 5. Ghost waypoint prevention
The tool must not materialize synthetic `contact` or event waypoints unless the user explicitly creates them.

Rules:
- propagation helpers may derive boxes, but they may not invent a new waypoint record without a create action
- event cleanup must remove orphan markers with no valid source boxes
- overlap with an existing object is not by itself a reason to create another waypoint

## Component Impact
Primary code areas likely affected:

- event waypoint creation and exit handlers
- event bbox propagation and interpolation paths
- vehicle matching and overlap resolution helpers
- list selection and deletion handlers
- waypoint cleanup and derived box pruning
- timeline rendering logic that decides whether an event remains visible

Given the current repository shape, most of this work will likely live in `Form1` partials related to timeline and JSON behavior rather than a new subsystem.

## Requirement Themes

### Theme A: Event segment uniqueness
The same user action should yield one event waypoint, not two.

### Theme B: Exit-bounded visibility
Event boxes must disappear at the event exit frame regardless of prior overlap or propagation.

### Theme C: Vehicle identity stability
Vehicles touched by event workflows must keep one consistent identity and one logical track.

### Theme D: Safe selection and deletion
The selected waypoint in the UI must be the waypoint that gets deleted or edited.

### Theme E: Regression confidence
Known PPT scenarios should become repeatable checks, not one-off fixes.

## Risks
- Logic may still be duplicated across multiple `Form1` partial methods, so a missed path could leave one bug variant alive.
- Legacy assisted labeling flows may depend on behavior that looked wrong but was being used as a workaround.
- Vehicle overlap logic may have side effects on non-event tracking if helper matching is too aggressive.

## Mitigations
- patch shared helpers first, then route specialized handlers through them
- test each reproduced PPT scenario explicitly
- prefer clamping and deduplication rules over inventing new side effects
- keep milestone scope focused on reproduced event stability issues

## Verification Plan
Minimum validation set for this milestone:

1. Event exit entered by button or `X` creates exactly one event waypoint.
2. Drawing a bbox on the exit frame does not create a duplicate event waypoint.
3. Event boxes disappear on or before the exit frame and do not persist until the overlapped object ends.
4. Auto-track and interpolation do not create duplicate event or vehicle boxes at event entry or exit.
5. Vehicle IDs stay stable for overlapping event scenarios.
6. Deleting a selected event or vehicle waypoint never deletes a previously selected person waypoint.
7. Ghost event or `contact` waypoints do not remain after cleanup or deletion.

## Deliverable Shape
The milestone should end with:

- scoped requirements in `.planning/REQUIREMENTS.md`
- a phased stabilization roadmap in `.planning/ROADMAP.md`
- an execution-ready milestone state in `.planning/STATE.md`

This scope is focused enough for one stabilization milestone and broad enough to eliminate the linked failures shown in the PPT.
