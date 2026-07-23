---
status: pending
scope: gsd-quick changes through 2026-07-23
scenario_file: QUICK-UAT-SCENARIOS.md
---

# Quick Change UAT Checklist

Fill one status per row: `PASS`, `FAIL`, `BLOCKED`, or `NOT RUN`.

| ID | Check | Status | Evidence / notes |
|---|---|---|---|
| Q-01.1 | Object Info Korean text renders correctly. | NOT RUN | |
| Q-01.2 | Person, vehicle, and event panels show readable Korean text. | NOT RUN | |
| Q-02.1 | Event Entry cell moves to the waypoint EntryFrame. | NOT RUN | |
| Q-02.2 | Event Exit cell moves to the waypoint ExitFrame. | NOT RUN | |
| Q-02.3 | Event Object cell selects the matching entry-frame event box. | NOT RUN | |
| Q-03.1 | Dropdown order matches all eight required event IDs. | NOT RUN | |
| Q-03.2 | New save uses IDs 25-32 for the new event order. | NOT RUN | |
| Q-03.3 | New save includes `event_camouflage` as ID 32. | NOT RUN | |
| Q-03.4 | New save excludes retired `event_exchange`, `event_board`, and `event_disembark`. | NOT RUN | |
| Q-04.1 | Legacy `event_exchange` reloads and re-saves as `event_throw`. | NOT RUN | |
| Q-04.2 | Legacy `event_board` reloads and re-saves as `event_get on`. | NOT RUN | |
| Q-04.3 | Legacy `event_disembark` reloads and re-saves as `event_get off`. | NOT RUN | |
| Q-04.4 | Legacy `event_camouflage` remains `event_camouflage`. | NOT RUN | |
| Q-04.5 | Legacy `event_throw` remains `event_throw`. | NOT RUN | |

## Automated Evidence

| Check | Result |
|---|---|
| Regression harness | PASS: 66 tests after the event catalog changes. |
| Debug x64 build | PASS: 0 errors using an isolated output directory. |

## Sign-Off

| Role | Name | Date | Result |
|---|---|---|---|
| QA | | | |
| Development | | | |
