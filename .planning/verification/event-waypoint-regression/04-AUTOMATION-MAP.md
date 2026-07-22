# Phase 4 Automation Map

**Prepared:** 2026-07-22  
**Purpose:** Classify each PPT scenario as `Automated`, `Hybrid`, or `Manual`, and link it to available evidence.

## Legend

- `Automated`: Can be judged from existing lightweight tests or deterministic non-UI commands.
- `Hybrid`: Needs automated evidence plus a manual UI replay or visual check.
- `Manual`: Requires direct app interaction and cannot be judged reliably from current code-level seams alone.

| PPT Item | Scenario | Mode | Evidence |
|----------|----------|------|----------|
| 1 | Duplicate event waypoint creation | `Hybrid` | Build + `WinFormsApp1.Tests` event finalization helper tests, then manual replay of event finalization flow |
| 2 | Event interacting object editor lands on exit column | `Manual` | UI list editing only; verify interacting-object text box appears on the correct column |
| 3 | Event box persists beyond exit | `Hybrid` | Build + event clamp tests, then manual replay with exit-frame and post-exit frame inspection |
| 4 | Entry + `E/X` path single-waypoint behavior | `Hybrid` | Build + helper tests, then manual replay with `E`/`X` flow |
| 5 | Vehicle overlap confirm behavior | `Manual` | Requires overlap setup and UI prompt observation |
| 6 | No exit-frame bbox keeps single event waypoint | `Manual` | Safe baseline path must be replayed in app |
| 7 | Frame-unit deletion removes undeletable event waypoint | `Hybrid` | Build + cleanup helper tests, then manual frame deletion flow |
| 8 | Event persists until overlapped object exit and cannot be deleted | `Hybrid` | Build + clamp/cleanup tests, then manual overlap scenario replay |
| 9 | Stale person selection deletes wrong waypoint | `Hybrid` | Build + list ownership helper tests, then manual delete attempt with stale selection |
| 10 | Exit-frame bbox + auto track duplicates vehicle entry boxes | `Manual` | Requires runtime overlap + auto-track setup in app |
| 11 | Same vehicle splits into `01/02` identities | `Hybrid` | Build + vehicle identity tests, then manual overlap/entry-exit replay |
| 12 | Exit-frame bbox + auto track duplicates event and vehicle tracking | `Manual` | Requires runtime auto-track and event workflow replay |
| 12-2 | Motorcycle/bicycle duplication variant | `Manual` | Requires target class and runtime replay |
| 13 | Ghost `contact` waypoint persists | `Manual` | Requires runtime scenario and visual observation |

## Current Automated Evidence

Primary automated command:

```powershell
C:\Program Files\dotnet\dotnet.exe C:\Users\ANNA\Documents\ETRILabelingTools\WinFormsApp1.Tests\bin\Debug\net8.0-windows\WinFormsApp1.Tests.dll
```

Relevant existing test signals:

- `pending event range reuses existing event instance id`
- `pending event range ignores different event ids`
- `event clamp trims only matching instance overflow`
- `event clamp can trim all waypoint overflows`
- `vehicle tracking comparison uses effective identity`
- `vehicle waypoint matching includes body and linked plate`
- `event workflow skips vehicle waypoint side effects`
- `active list owner prefers current list selection`
- `active list owner falls back to remaining selected list`

## Why Some Items Stay Manual

- The current harness is logic-level and intentionally lightweight.
- UI-heavy flows such as popup timing, exact overlap setup, and waypoint list inline editing do not yet have dedicated UI automation seams.
- Phase 4 intentionally avoids introducing a heavyweight UI automation framework just to force full automation.

## Reuse Notes

- When new automated tests are added in future milestones, update this map first, then link the new evidence into `04-PPT-VERIFICATION.md`.
- Manual items can later be downgraded to `Hybrid` or `Automated` if the codebase gains appropriate harness seams.
