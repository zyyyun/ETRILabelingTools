---
phase: 06-prevent-stale-event-boxes-after-waypoint-box-edits
reviewed: 2026-07-23T00:00:00Z
depth: standard
files_reviewed: 8
files_reviewed_list:
  - WinFormsApp1/Logic/EventWaypointBoxPropagationHelper.cs
  - WinFormsApp1/Logic/EventRectanglePropagationUndoHelper.cs
  - WinFormsApp1/Logic/EventTombstoneUndoHelper.cs
  - WinFormsApp1/Form1.cs
  - WinFormsApp1/Forms/Form1.Undo.cs
  - WinFormsApp1/Forms/Form1.EventPropagation.cs
  - WinFormsApp1/Forms/Form1.Drawing.cs
  - WinFormsApp1.Tests/Program.cs
findings:
  critical: 0
  warning: 0
  info: 0
  total: 0
status: clean
---

# Phase 06: 코드 재검토 보고서

**검토 시각:** 2026-07-23T00:00:00Z
**깊이:** standard
**검토 파일:** 8개
**상태:** clean

## Summary

CR-01~CR-04 및 WR-01~WR-02의 수정 사항을 재검토했다. 실제 사각형 변경 시에만 전파와 이력이 생성되며, 전파 배치는 수동 보정 프레임의 이전 상태를 포함해 Undo/Redo에서 원자적으로 복원한다. 전파 대상이 없어도 source-only 이력이 생성되고 Event UI가 갱신된다. 이벤트 삭제는 원래 tombstone 객체를 재활성화/재삭제하며, 도달 불가능했던 레거시 전파 블록은 제거됐다.

`dotnet build ETRILabelingTool.sln -c Debug -p:Platform=x64` 및 `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj -c Debug`를 실행했으며, 빌드 경고·오류 없이 65개 테스트가 모두 통과했다.

## Narrative Findings (AI reviewer)

재검토 범위에서 수정되지 않은 BLOCKER, WARNING 또는 INFO 항목을 발견하지 못했다.

### 기존 지적 검증

- CR-01: 드래그/리사이즈 종료 시 `HasGeometryChanged`가 참일 때만 수동 보정 기록·전파·Undo 이력을 생성한다.
- CR-02: `EventManualFrameProvenance`가 전파 배치에 저장되고 Undo/Redo에서 수동-보정 상태와 함께 복원된다.
- CR-03: 빈 전파 계획도 source-only 배치로 기록하며 Event 관련 화면을 갱신한다.
- CR-04: 이벤트 삭제 Undo/Redo는 clone을 추가·제거하지 않고 원래 tombstone 객체의 `IsDeleted`만 전환한다.
- WR-01: tombstone Undo/Redo 후 `RefreshEventSurfaces()`를 호출한다.
- WR-02: 기존 EventId 기반 레거시 전파 블록이 제거됐다.

---

_검토 시각: 2026-07-23T00:00:00Z_
_검토자: the agent (gsd-code-reviewer)_
_깊이: standard_
