---
phase: 06
slug: prevent-stale-event-boxes-after-waypoint-box-edits
status: draft
nyquist_compliant: true
wave_0_complete: false
created: 2026-07-23
---

# Phase 06 - Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | Existing `WinFormsApp1.Tests` console regression harness |
| **Config file** | None; tests are registered in `WinFormsApp1.Tests/Program.cs` |
| **Quick run command** | `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` |
| **Full suite command** | `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` then `dotnet build ETRILabelingTool.sln -c Debug -p:Platform=x64` |
| **Estimated runtime** | Under 60 seconds for the harness; build duration depends on local restore state |

## Sampling Rate

- **After every task commit:** Run `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore`
- **After every plan wave:** Run the full suite command above.
- **Before `$gsd-verify-work`:** Full suite must be green and the manual UI smoke check must be recorded.
- **Max feedback latency:** 60 seconds for automated logic checks.

## Per-Task Verification Map

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 06-01-01 | 01 | 1 | D-01, D-02, D-03 | T-06-01 | Only same-instance derived boxes after the source frame update; later manual frames remain unchanged. | unit | `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` | Wave 0 | pending |
| 06-01-02 | 01 | 1 | D-04, D-05 | T-06-02 | A deleted frame remains deleted and no later box is removed by a single-frame deletion. | unit | `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` | Wave 0 | pending |
| 06-02-01 | 02 | 2 | D-06 | T-06-03 | Drag, resize, and single-frame delete refresh the canvas, Event list, and waypoint list. | manual UI smoke | N/A | Existing UI | pending |

## Wave 0 Requirements

- [ ] Add helper-level regression cases to `WinFormsApp1.Tests/Program.cs` for forward-only propagation, manual-frame protection, tombstone preservation, and `EventInstanceId` isolation.
- [ ] Expose a non-UI propagation seam in `WinFormsApp1/Logic/EventWaypointBoxPropagationHelper.cs` or extend `EventWaypointUpdateHelper.cs` so the cases can run without a WinForms form.

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Immediate refresh after event-box drag or resize | D-06 | The harness does not host the full WinForms painting and list-control lifecycle. | Edit an event rectangle; without navigating frames, confirm the canvas and selected Event Waypoint row show the new state. |
| Single-frame event-box deletion | D-04, D-06 | Requires confirming visual removal on the current frame while later frames remain present. | Delete one event box, inspect the current frame, then move to the next frame and confirm its box remains. |

## Validation Sign-Off

- [ ] All tasks have automated verification or a declared manual-only reason.
- [ ] Sampling continuity: no three consecutive tasks without automated verification.
- [ ] Wave 0 covers all helper-level behavior.
- [ ] No watch-mode flags are used.
- [ ] Feedback latency is under 60 seconds for automated checks.
- [x] `nyquist_compliant: true` is set in frontmatter.

**Approval:** pending
