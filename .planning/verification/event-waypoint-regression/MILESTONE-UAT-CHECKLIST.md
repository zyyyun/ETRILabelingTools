---
status: pending
scope: phase-04, phase-05, phase-06, phase-08, gsd-quick
scenario_file: MILESTONE-UAT-SCENARIOS.md
---

# Milestone And Quick Change UAT Checklist

Use `PASS`, `FAIL`, `BLOCKED`, or `NOT RUN`. A failure must include the evidence
requested in the scenario file.

| ID | Scope | Check | Status | Evidence / notes |
|---|---|---|---|---|
| UAT-01.1 | Phase 4 | Event type change reaches every active box in the selected EventInstanceId. | NOT RUN | |
| UAT-01.2 | Phase 4 | Same-type boxes in another EventInstanceId remain unchanged. | NOT RUN | |
| UAT-01.3 | Phase 4 | Event row and Event panel refresh immediately. | NOT RUN | |
| UAT-01.4 | Phase 4 | One Undo/Redo restores/reapplies all changed boxes. | NOT RUN | |
| UAT-02.1 | Phase 5 | Event columns are Entry, Exit, Object. | NOT RUN | |
| UAT-02.2 | Phase 5 | Entry/Exit show video time. | NOT RUN | |
| UAT-02.3 | Phase 5 + quick | Exit cell moves to ExitFrame. | NOT RUN | |
| UAT-02.4 | Phase 5 | Object-only inline editor does not open on Exit. | NOT RUN | |
| UAT-02.5 | Phase 5 | Active list selection prevents stale Person deletion. | NOT RUN | |
| UAT-03.1 | PPT G1 | Entry/Exit and X create one Event waypoint. | NOT RUN | |
| UAT-03.2 | PPT G2 | Event box disappears after its ExitFrame. | NOT RUN | |
| UAT-03.3 | PPT G4 | Frame-level event deletion leaves no orphan/ghost waypoint. | NOT RUN | |
| UAT-03.4 | PPT G2/G4 | Overlap does not prolong or block event cleanup. | NOT RUN | |
| UAT-04.1 | Phase 6 | Edit updates only forward derived boxes in the same EventInstanceId. | NOT RUN | |
| UAT-04.2 | Phase 6 | Manual frames, other instances, and tombstones remain intact. | NOT RUN | |
| UAT-04.3 | Phase 6 | Single-frame delete leaves later event boxes intact. | NOT RUN | |
| UAT-04.4 | Phase 6 | Edit Undo/Redo is atomic and UI refreshes immediately. | NOT RUN | |
| UAT-05.1 | Phase 8 | Face/plate G deletes only the selected current-frame child. | NOT RUN | |
| UAT-05.2 | Phase 8 | Face/plate Delete scopes to the parent waypoint only. | NOT RUN | |
| UAT-05.3 | Phase 8 | Other children, bodies, and waypoints remain intact. | NOT RUN | |
| UAT-05.4 | Phase 8 | Undo and export preserve/restores the correct child-link state. | NOT RUN | |
| UAT-06.1 | Quick | Korean Object Info and labels render normally. | NOT RUN | |
| UAT-07.1 | Quick | Dropdown order contains the eight required event values. | NOT RUN | |
| UAT-07.2 | Quick | New save writes event category IDs 25-32. | NOT RUN | |
| UAT-07.3 | Quick | Legacy exchange, board, and disembark map to the approved replacements. | NOT RUN | |
| UAT-07.4 | Quick | Legacy camouflage and throw remain their own values. | NOT RUN | |

## Automated Baseline

| Check | Result |
|---|---|
| Regression harness | PASS: 66 tests after the quick event catalog changes. |
| Debug x64 build | PASS: 0 errors using an isolated output directory. |

## Sign-Off

| Role | Name | Date | Result |
|---|---|---|---|
| QA | | | |
| Development | | | |
