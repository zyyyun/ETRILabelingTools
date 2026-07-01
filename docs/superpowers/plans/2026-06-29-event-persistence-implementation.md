# Event Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Event 저장/재로드 시 같은 종류의 Event가 서로 합쳐지거나 사라지지 않도록 `event_instance_id` 기반의 안정 식별을 도입한다.

**Architecture:** 기존 JSON 구조는 유지하고 Event 전용 선택 필드 `event_instance_id`만 추가한다. 메모리 모델, 저장 경로, 로드 경로, Event 조작 경로를 모두 이 식별자 중심으로 통일해 휘발을 막는다.

**Tech Stack:** .NET 8 WinForms, C#, Newtonsoft.Json, 단일 메인 파일 `WinFormsApp1/Form1.cs`

---

### Task 1: Event 인스턴스 식별 모델 추가

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Test: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: Event 식별 관련 구조 위치 확인**

확인 대상:
- `BoundingBox`
- `WaypointMarker`
- `AnnotationData`
- Event 생성/복제 경로

찾아야 할 메서드:
- `FindWaypointForBox`
- `CloneBoundingBox`
- Event bbox 생성 지점

- [ ] **Step 2: Event 전용 식별 필드 추가**

추가 대상:
```csharp
public string EventInstanceId { get; set; }
```

반영 위치:
- `BoundingBox`
- `WaypointMarker`
- `AnnotationData`

주의:
- Person/Vehicle에는 사용하지 않는다.
- JSON 필드는 Event인 경우에만 쓰이도록 한다.

- [ ] **Step 3: Event 식별 헬퍼 추가**

추가할 최소 헬퍼 예시:
```csharp
private bool IsSameEventInstance(BoundingBox box, WaypointMarker waypoint)
{
    if (box == null || waypoint == null) return false;
    if (box.Label != "event" || waypoint.Label != "event") return false;

    if (!string.IsNullOrWhiteSpace(box.EventInstanceId) &&
        !string.IsNullOrWhiteSpace(waypoint.EventInstanceId))
    {
        return string.Equals(box.EventInstanceId, waypoint.EventInstanceId, StringComparison.Ordinal);
    }

    return box.EventId == waypoint.ObjectId &&
           box.FrameIndex >= waypoint.EntryFrame &&
           box.FrameIndex <= waypoint.ExitFrame;
}
```

- [ ] **Step 4: Event 인스턴스 ID 발급 헬퍼 추가**

예시:
```csharp
private string CreateEventInstanceId()
{
    return $"event-{Guid.NewGuid():N}";
}
```

규칙:
- Entry만 설정된 상태에서는 아직 확정하지 않는다.
- Event waypoint 확정 시점에 발급한다.

- [ ] **Step 5: Event 복제/전파 시 식별자 유지**

다음 코드 경로에서 `EventInstanceId`가 복사되도록 수정:
- `CloneBoundingBox`
- Event bbox 생성 코드
- Event 전파 코드
- Event 재추적/보간 코드 중 Event bbox를 새로 만드는 부분

- [ ] **Step 6: 빌드 확인**

Run: `dotnet build ETRILabelingTool.sln`
Expected: 새 컴파일 오류 없음

- [ ] **Step 7: Commit**

```bash
git add WinFormsApp1/Form1.cs
git commit -m "feat: add stable event instance model"
```

### Task 2: JSON 저장/로드 경로에 event_instance_id 연결

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Test: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: 저장 경로에서 Event annotation에 event_instance_id 기록**

수정 대상:
- `ExportToJsonExtended`

반영 규칙:
```csharp
if (box.Label == "event" && !string.IsNullOrWhiteSpace(box.EventInstanceId))
{
    annotation.EventInstanceId = box.EventInstanceId;
}
```

- [ ] **Step 2: Waypoint 우선 매칭도 EventInstanceId 기준 추가**

현재 `matchingWaypoint` 탐색 로직을 보완한다.
우선순위:
1. `event_instance_id` 일치
2. 기존 fallback 규칙

- [ ] **Step 3: 로드 경로에서 event_instance_id 복원**

수정 대상:
- `LoadLabelingData`
- temp bounding box 생성
- temp waypoint 생성

반영 규칙:
```csharp
EventInstanceId = label == "event" ? annotation.EventInstanceId : null;
```

- [ ] **Step 4: waypointKey를 event_instance_id 우선으로 변경**

현재 key 생성이 `label/objectId/entry/exit` 중심이면 Event에서 충돌할 수 있다.
Event는 다음 우선순위로 key 구성:
1. `event_instance_id`
2. 기존 fallback key

예시:
```csharp
string waypointKey = box.Label == "event" && !string.IsNullOrWhiteSpace(box.EventInstanceId)
    ? $"event_{box.EventInstanceId}"
    : $"{box.Label}_{objectId}_{entryFrame}_{exitFrame}";
```

- [ ] **Step 5: 레거시 파일 fallback 부여 로직 추가**

`event_instance_id`가 없는 Event annotation은 메모리에서 새 식별자를 부여한다.
단, 같은 레거시 Event 그룹에는 같은 식별자가 들어가야 한다.
그룹 기준:
- Event 종류
- Entry/Exit
- interacting_object
- bbox 연속성

- [ ] **Step 6: 저장 후 재로드 경로 점검**

저장 직후 `LoadLabelingData(currentVideoFile)` 또는 같은 JSON 재로드 경로에서 Event 인스턴스가 유지되는지 확인할 수 있도록 로그를 보완한다.

- [ ] **Step 7: 빌드 확인**

Run: `dotnet build ETRILabelingTool.sln`
Expected: 새 컴파일 오류 없음

- [ ] **Step 8: Commit**

```bash
git add WinFormsApp1/Form1.cs
git commit -m "feat: persist event instance ids in json"
```

### Task 3: Event 조작 경로를 event_instance_id 기준으로 통일

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Test: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: FindWaypointForBox를 Event 우선 분기 추가**

수정 규칙:
- Event이며 `EventInstanceId`가 있으면 같은 `EventInstanceId`를 가진 waypoint를 우선 반환
- 없으면 기존 fallback 사용

- [ ] **Step 2: Event Exit 확정/단축 로직 수정**

수정 대상:
- `SetExitMarkerAndCreateWaypoint`

변경 포인트:
- 기존 Event waypoint 찾기를 `EventId` 단독이 아니라 `EventInstanceId` 우선으로 변경
- Event 구간 삭제/전파 범위 계산도 같은 인스턴스만 대상으로 제한

- [ ] **Step 3: Event 종료(Q키) 로직 수정**

수정 대상:
- `TerminateEventFromCurrentFrame`

변경 포인트:
- 현재 프레임의 Event 여러 개 중 선택된 Event 인스턴스를 명확히 구분
- 삭제 대상 bbox 검색 시 `EventInstanceId` 우선 비교 사용
- waypoint 종료도 같은 인스턴스 하나만 갱신

- [ ] **Step 4: Event 전파 경로 수정**

수정 대상:
- `PropagateEventBoxWithinRange`

변경 포인트:
- 새로 생성되는 Event bbox에 `EventInstanceId` 유지
- 동일 프레임 기존 Event와 충돌 시 같은 인스턴스인지 확인 후 처리

- [ ] **Step 5: Event 관련 재추적/보간 경로 점검**

Event bbox를 새로 만드는 모든 경로에서 `EventInstanceId`가 누락되지 않도록 확인하고 보완한다.

- [ ] **Step 6: 최소 수동 검증 로그 추가**

Debug 로그 예시:
```csharp
System.Diagnostics.Debug.WriteLine($"[EventInstance] id={box.EventInstanceId}, eventType={box.EventId}, frame={box.FrameIndex}");
```

- [ ] **Step 7: 빌드 확인**

Run: `dotnet build ETRILabelingTool.sln`
Expected: 새 컴파일 오류 없음

- [ ] **Step 8: Commit**

```bash
git add WinFormsApp1/Form1.cs
git commit -m "fix: stabilize event waypoint operations by instance id"
```

### Task 4: 최종 검증 및 회귀 확인

**Files:**
- Modify: `WinFormsApp1/Form1.cs` (필요 시 로그 정리만)
- Test: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: 코드 전역 검색으로 EventId 단독 식별 경로 재점검**

검색 대상 예시:
- `Label == "event"`
- `EventId ==`
- `ObjectId ==`
- `FindWaypointForBox`

목표:
- Event 인스턴스를 찾는 코드가 여전히 `EventId`만 기준으로 남아 있는지 확인

- [ ] **Step 2: 빌드 실행**

Run: `dotnet build ETRILabelingTool.sln`
Expected: PASS 또는 실행 중 프로세스 잠금만 존재

- [ ] **Step 3: 수동 검증 체크리스트 수행**

검증 시나리오:
- 같은 종류 `contact` Event를 서로 다른 두 구간에 생성
- 저장 후 재로드
- 두 Event가 모두 유지되는지 확인
- 한쪽 Exit를 줄인 뒤 다시 저장/재로드
- 다른 Event가 영향받지 않는지 확인
- 구버전 JSON을 열고 다시 저장한 뒤 안정적으로 유지되는지 확인

- [ ] **Step 4: 필요 시 디버그 로그 정리**

임시 로그가 과하면 최소한으로 줄인다.

- [ ] **Step 5: Commit**

```bash
git add WinFormsApp1/Form1.cs
git commit -m "test: verify event persistence stability"
```