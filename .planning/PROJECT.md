# ETRI Labeling Tool

Last updated: 2026-07-21

## What This Is

ETRI Labeling Tool is a Windows Forms video labeling application used to annotate people, vehicles, and events across video frames. The highest-risk workflows are waypoint creation, auto-tracking, interpolation, and JSON round-tripping because a small state mismatch can corrupt a long labeling session.

## Core Value

Labeling stability over convenience. A slightly slower workflow is acceptable if it prevents duplicated waypoints, wrong deletions, or corrupted event ranges.

## Current Milestone: v1.0 Event Waypoint Stabilization

**Goal:** Remove reproduced Event Waypoint regressions from the July 20, 2026 fix request deck and make event-assisted labeling deterministic.

**Target features:**
- Single-source event waypoint creation and exit confirmation
- Event visibility clamped to the declared exit frame
- Stable vehicle identity during event-assisted generation
- Safe selection and deletion across person, vehicle, and event lists
- Regression verification for reproduced PPT scenarios

## Active Requirements

- `EVT-01` through `EVT-05`
- `TRK-01` through `TRK-03`
- `UI-01` through `UI-03`
- `QA-01` through `QA-03`

## Validated Requirements

None yet. This milestone has not started execution.

## Out of Scope

- Large-scale `Form1` refactor
- New annotation modes unrelated to event stabilization
- JSON schema redesign beyond the existing event stability work
- Broad UI redesign outside selection and deletion safety

## Key Decisions

- Use a stabilization milestone instead of isolated hotfixes.
- Keep the work focused on Event Waypoint lifecycle, vehicle coupling, and selection safety.
- Treat the July 20, 2026 PPT as the primary reproduction source for requirement scope.

## Context

- The repository currently has no prior `.planning` structure, so this milestone bootstraps GSD planning artifacts for the first time in this workspace.
- Existing design docs already cover event persistence and adjacent labeling features, which lowers design uncertainty but not regression risk.
- Current source changes exist outside `.planning`; milestone documents are being added without rewriting user code changes.

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition:**
1. Requirements invalidated: move them to Out of Scope with reason.
2. Requirements validated: move them to Validated with phase reference.
3. New requirements emerged: add them to Active Requirements.
4. Decisions to log: add them to Key Decisions.
5. If the product description drifted, update What This Is.

**After each milestone:**
1. Review all sections for drift.
2. Re-check the Core Value.
3. Audit Out of Scope items for continued relevance.
4. Refresh Context with the latest working state.
