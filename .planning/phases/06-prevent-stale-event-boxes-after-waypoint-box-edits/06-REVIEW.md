---
phase: 06-prevent-stale-event-boxes-after-waypoint-box-edits
reviewed: 2026-07-23T00:00:00Z
depth: standard
files_reviewed: 7
files_reviewed_list:
  - WinFormsApp1/Logic/EventWaypointBoxPropagationHelper.cs
  - WinFormsApp1/Logic/EventRectanglePropagationUndoHelper.cs
  - WinFormsApp1/Form1.cs
  - WinFormsApp1/Forms/Form1.Undo.cs
  - WinFormsApp1/Forms/Form1.EventPropagation.cs
  - WinFormsApp1/Forms/Form1.Drawing.cs
  - WinFormsApp1.Tests/Program.cs
findings:
  critical: 4
  warning: 2
  info: 0
  total: 6
status: issues_found
---

# Phase 06: Code Review Report

**Reviewed:** 2026-07-23T00:00:00Z
**Depth:** standard
**Files Reviewed:** 7
**Status:** issues_found

## Summary

이벤트 인스턴스 격리와 기본 전파 계획은 적절히 fail-closed로 구현되어 있고, 자동 회귀 62건 및 Debug 빌드는 통과했다. 그러나 실제 WinForms 편집 경로에서는 단순 선택이 수동 보정으로 기록되고, 그 상태 및 프레임 삭제 tombstone이 Undo/Redo와 원자적으로 복원되지 않는다. 그 결과 이후의 앞선 프레임 편집이 복원된 박스를 갱신하지 못하거나, 전파 대상이 하나도 없을 때 사용자의 편집 자체를 되돌릴 수 없다.

## Narrative Findings (AI reviewer)

## Critical Issues

### CR-01: 단순 선택 클릭이 이후 전파를 영구 차단함

**File:** `WinFormsApp1/Forms/Form1.Drawing.cs:199-202, 450-461`
**Classification:** BLOCKER
**Issue:** 박스를 클릭해 선택만 해도 `isDragging`이 설정되고, MouseUp에서 실제 `Rectangle` 변경 여부를 확인하지 않은 채 `RecordManuallyAdjustedFrame`을 호출한다. Phase 6 전파 계획은 이 목록을 절대 보호 대상으로 사용하므로, 사용자가 나중 프레임 박스를 단순 선택한 뒤 앞 프레임을 편집하면 선택만 했던 나중 프레임이 수동 보정으로 오인되어 갱신되지 않는다. D-03의 “실제 수동 보정만 보호” 규칙을 위반한다.

**Fix:** MouseUp/resize 완료 시 `selectedBox.Rectangle != originalDragRect` 또는 `!= originalResizeRect`일 때만 수동 프레임 기록, 전파, Undo 액션 생성을 수행한다. 변경이 없으면 선택 UI만 갱신한다.

### CR-02: 이벤트 편집 Undo/Redo가 수동 보호 상태를 복원하지 않음

**File:** `WinFormsApp1/Forms/Form1.Drawing.cs:422, 456; WinFormsApp1/Forms/Form1.Undo.cs:86-91, 163-168`
**Classification:** BLOCKER
**Issue:** 편집 직후 수동 프레임 목록이 변경되지만 `EventRectanglePropagationBatch`에는 그 전/후 상태가 없고 Undo/Redo도 목록을 건드리지 않는다. 따라서 이벤트 박스 편집을 Undo한 뒤 앞 프레임을 다시 편집하면, 이미 취소된 프레임이 여전히 수동 보정으로 남아 전파에서 제외된다. Redo도 반대 상태를 재현하지 못해 “한 번의 Undo/Redo가 전파 편집 전체를 복원”한다는 원자성 요구를 충족하지 못한다.

**Fix:** 배치에 identity key와 해당 frame의 수동-보정 기록 전/후 상태를 저장하고, `ApplyUndo`/`ApplyForward`와 같은 트랜잭션에서 목록도 복원한다. 이 동작을 포함하는 회귀 테스트(편집 → Undo → 앞 프레임 편집)를 추가한다.

### CR-03: 전파 대상이 없으면 사용자의 이벤트 박스 편집이 Undo 불가

**File:** `WinFormsApp1/Forms/Form1.EventPropagation.cs:433-446`
**Classification:** BLOCKER
**Issue:** 현재 프레임이 waypoint 종료 프레임이거나 이후의 모든 프레임이 수동 보정/tombstone이면 계획의 updates/additions가 비어 즉시 return한다. 하지만 source rectangle은 이미 MouseMove에서 변경됐으므로, 이 경우 Undo 액션이 전혀 만들어지지 않는다. 사용자는 실제로 수정한 이벤트 박스를 되돌릴 수 없고, 빈 계획 경로에서는 Event/Waypoint UI도 갱신되지 않아 D-06도 실패한다.

**Fix:** source 변경 여부를 먼저 검사하고, 대상 변경이 0개여도 source-only `EventRectanglePropagationBatch`를 추가한다. 그 뒤 항상 `RefreshEventSurfaces()`를 호출한다. 변경이 없었던 클릭은 CR-01의 조건으로 no-op 처리한다.

### CR-04: 프레임 삭제 Undo가 tombstone을 남긴 채 복제 박스를 추가함

**File:** `WinFormsApp1/Forms/Form1.Drawing.cs:2771-2775; WinFormsApp1/Forms/Form1.Undo.cs:42-44, 117-122`
**Classification:** BLOCKER
**Issue:** 삭제는 원본 객체를 목록에 남기고 `IsDeleted = true`로 tombstone을 만든다. 그러나 Undo는 원본 tombstone을 되살리지 않고 clone을 추가한다. 같은 프레임에 활성 clone과 삭제 tombstone이 공존하며, `PlanPropagation`은 해당 프레임에 tombstone이 하나라도 있으면 전파를 차단한다. 즉 삭제를 Undo한 뒤 앞 프레임을 수정해도 복원된 박스는 갱신되지 않는다. Redo 역시 clone만 제거하여 원본 tombstone을 대상으로 한 가역 이력이 아니다.

**Fix:** `RemoveBox` Undo 액션에 원본 객체 참조와 삭제 전 상태를 저장하고 Undo에서는 그 객체의 `IsDeleted`를 false로, Redo에서는 true로 되돌린다. 동일 프레임의 active/tombstone 중복을 금지하는 회귀 테스트와 “삭제 → Undo → 앞 프레임 편집” 테스트를 추가한다.

## Warnings

### WR-01: 삭제 Undo/Redo 시 Event UI가 즉시 새로고침되지 않음

**File:** `WinFormsApp1/Forms/Form1.Undo.cs:42-45, 117-122`
**Classification:** WARNING
**Issue:** 직접 삭제는 `RefreshEventSurfaces()`를 호출하지만, 그 삭제의 Undo/Redo는 일반 `RemoveBox` 분기만 실행해 Event list와 Event Waypoint list를 갱신하지 않는다. 사용자는 삭제/복구 후 이동하기 전까지 이벤트 패널의 오래된 항목을 볼 수 있다.

**Fix:** `RemoveBox` 액션에 이벤트 여부를 보존하거나 대상의 label을 검사해, 이벤트 삭제 Undo/Redo 뒤 `RefreshEventSurfaces()`(또는 동등한 Event/Waypoint 갱신)를 호출한다.

### WR-02: 새 전파 구현 뒤에 도달 불가능한 구 전파 코드가 남아 있음

**File:** `WinFormsApp1/Forms/Form1.EventPropagation.cs:447-534`
**Classification:** WARNING
**Issue:** line 447의 무조건 `return` 뒤에 이전 EventId 기반 전파 구현이 그대로 남아 있어 절대 실행되지 않는다. 이 코드는 현재의 인스턴스 격리·tombstone 보존 규칙과도 달라, 향후 return을 제거하거나 코드를 수정할 때 이전의 위험한 동작이 다시 활성화될 수 있다.

**Fix:** 도달 불가능한 기존 블록 전체를 삭제하고, 필요한 진단 로그만 새 planner 경로에 옮긴다.

---

_Reviewed: 2026-07-23T00:00:00Z_
_Reviewer: the agent (gsd-code-reviewer)_
_Depth: standard_
