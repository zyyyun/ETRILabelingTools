# Phase 4: Reproduce PPT Scenarios And Lock In Regression Coverage - Context

**Gathered:** 2026-07-22
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase converts the July 20, 2026 PPT issues into a reusable verification workflow. It defines how each PPT item is replayed, how results are judged, and how evidence is recorded. It does not introduce new runtime behavior unless a verification artifact or lightweight harness is required to support repeatable regression checks.

</domain>

<decisions>
## Implementation Decisions

### Verification unit
- **D-01:** The primary verification unit is the original PPT item number.
- **D-02:** In addition to per-item checks, closely related issues may be grouped into bundled scenario runs when they share a root cause or workflow path.
- **D-03:** The verification output must preserve traceability back to the PPT numbering even when grouped scenarios are used.

### Result judgment
- **D-04:** Verification outcomes use exactly three statuses: `Fixed`, `Partial`, and `Blocked`.
- **D-05:** `Fixed` means the PPT item's core symptom cannot be reproduced in the target flow.
- **D-06:** `Partial` means the core symptom is reduced or constrained, but residual issues, workarounds, or edge-case failures remain.
- **D-07:** `Blocked` means the team cannot complete a valid judgment because of missing data, environment limits, or another blocking defect.

### Evidence format
- **D-08:** Verification results are recorded in a structured artifact, not a loose checklist.
- **D-09:** Each verification record must include: `PPT item`, `reproduction steps`, `expected result`, `actual result`, `status`, and `notes`.
- **D-10:** The final artifact should be easy to rerun later with the same steps and compare against future builds.

### the agent's Discretion
- The exact file split for the verification artifact and any helper checklist files.
- Whether bundled scenarios are represented inline in one report or as linked sub-sections, as long as PPT traceability stays explicit.

</decisions>

<specifics>
## Specific Ideas

- The source PPT is `260720_툴 수정 사항.pptx`, and the text/image extraction work already done in this workspace should guide the verification catalog.
- Important examples called out during discussion include event duplication, post-exit persistence, ghost waypoint cleanup, vehicle split IDs, and wrong-target deletion from stale selection.

</specifics>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Milestone scope
- `.planning/PROJECT.md` - Milestone goal and scope boundaries.
- `.planning/REQUIREMENTS.md` - Phase-mapped requirements `QA-01`, `QA-02`, and `QA-03`.
- `.planning/ROADMAP.md` - Phase 4 goal and success criteria.
- `.planning/STATE.md` - Current handoff state after Phase 3.

### Prior phase outcomes
- `.planning/phases/01-normalize-event-creation-and-exit-finalization/01-01-SUMMARY.md` - Event creation/finalization fixes implemented.
- `.planning/phases/01-normalize-event-creation-and-exit-finalization/01-02-SUMMARY.md` - Event helper guard outcomes.
- `.planning/phases/02-clamp-event-lifetime-and-cleanup-derived-artifacts/02-01-SUMMARY.md` - Event lifetime trim behavior.
- `.planning/phases/02-clamp-event-lifetime-and-cleanup-derived-artifacts/02-02-SUMMARY.md` - Manual-tracking-first event cleanup outcomes.
- `.planning/phases/03-stabilize-vehicle-coupling-and-selection-safety/03-01-SUMMARY.md` - Vehicle identity normalization outcomes.
- `.planning/phases/03-stabilize-vehicle-coupling-and-selection-safety/03-02-SUMMARY.md` - Event-assisted vehicle duplication prevention outcomes.
- `.planning/phases/03-stabilize-vehicle-coupling-and-selection-safety/03-03-SUMMARY.md` - Active list ownership and delete-safety outcomes.

### Source material
- `docs/superpowers/specs/2026-07-21-event-waypoint-stabilization-design.md` - Original stabilization scope and issue grouping.
- `C:/Users/ANNA/Downloads/260720_툴 수정 사항.pptx` - Original field issue deck.
- `C:/Users/ANNA/.codex/visualizations/2026/07/21/019f839c-2650-7b82-8d82-3c3846e47b0f/ppt_render/slides/슬라이드1.PNG` - Rendered slide evidence set (representative path pattern for all rendered slides).

### Relevant code and tests
- `WinFormsApp1.Tests/Program.cs` - Existing lightweight regression harness that may be extended or referenced.
- `WinFormsApp1/Forms/Form1.EntryExit.cs` - Event creation/finalization logic under test.
- `WinFormsApp1/Forms/Form1.EventPropagation.cs` - Event helper behavior under test.
- `WinFormsApp1/Forms/Form1.Json.cs` - Event termination and save-time clamp behavior under test.
- `WinFormsApp1/Forms/Form1.Timeline.cs` - Selection and delete behavior under test.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `WinFormsApp1.Tests/Program.cs` already holds targeted regression-style checks and is a natural place for lightweight automated hooks.
- `.planning/phases/*-SUMMARY.md` files already summarize implemented fixes by phase, which makes them good input for mapping PPT items to expected outcomes.
- Rendered PPT slides and extracted media already exist in the workspace, reducing ambiguity when building the verification catalog.

### Established Patterns
- Earlier phases used summary documents plus direct build/test evidence rather than a formal verification matrix.
- The current repo already mixes automated checks and manual UI validation, so Phase 4 should explicitly separate which items are automated, manual, or hybrid.

### Integration Points
- The verification artifact should map each PPT item to the phase(s) that addressed it.
- If a lightweight automated hook is added, it should complement, not replace, the structured manual verification record.
- Future `$gsd-progress` and milestone completion steps should be able to reference the Phase 4 verification artifact as the main acceptance report.

</code_context>

<deferred>
## Deferred Ideas

- Full automation of every PPT scenario is out of scope if the current app architecture makes UI replay too expensive.
- A new dedicated verification app or test runner is out of scope unless the planner proves the existing harness cannot support the required checks.

</deferred>

---

*Phase: 04-reproduce-ppt-scenarios-and-lock-in-regression-coverage*
*Context gathered: 2026-07-22*
