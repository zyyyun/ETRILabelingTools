---
phase: 04
slug: synchronize-event-ids-across-waypoint-segments
status: draft
nyquist_compliant: true
wave_0_complete: true
created: 2026-07-22
---

# Phase 4 Validation Strategy

## Test Infrastructure

| Property | Value |
|----------|-------|
| Framework | Lightweight .NET executable harness |
| Config file | `WinFormsApp1.Tests/WinFormsApp1.Tests.csproj` |
| Quick run command | `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj` |
| Full suite command | `dotnet build ETRILabelingTool.sln -c Debug -p:Platform=x64` then `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj` |
| Estimated runtime | ~30 seconds |

## Sampling Rate

- After every task commit: run the test harness.
- After the implementation wave: build the solution and run the test harness.
- Before Verify Phase: full suite must be green; manual PPT/UAT replay is a separate milestone gate.
- Max feedback latency: 30 seconds.

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Test Type | Automated Command | Status |
|---------|------|------|-------------|-----------|-------------------|--------|
| 04-01-01 | 01 | 1 | EVT-06 | unit | `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj` | pending |
| 04-01-02 | 01 | 1 | EVT-06 | integration/build | `dotnet build ETRILabelingTool.sln -c Debug -p:Platform=x64` | pending |

## Wave 0 Requirements

Existing test infrastructure covers the phase. Add event waypoint update cases
to `WinFormsApp1.Tests/Program.cs` with the extracted non-UI helper.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| List and current-frame panel refresh immediately | EVT-06 | WinForms controls are not exercised by the lightweight harness | Change one event type in a multi-frame waypoint and confirm every active box plus both UI views show the new type. |
| Atomic undo/redo through keyboard/UI | EVT-06 | Undo stack is private Form state | Change one event type, invoke Undo once, confirm all boxes restore; invoke Redo once, confirm all active boxes update again. |

## Validation Sign-Off

- [ ] All task commits run the automated test harness.
- [ ] Build succeeds with x64 Debug configuration.
- [ ] Manual UI refresh and atomic undo/redo checks pass.
- [x] No watch-mode commands.

**Approval:** pending
