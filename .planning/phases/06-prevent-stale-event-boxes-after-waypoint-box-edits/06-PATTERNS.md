# Phase 6: Prevent stale event boxes after waypoint box edits - Pattern Map

**매핑일:** 2026-07-23  
**분석 대상 파일:** 4개  
**아날로그 발견:** 4 / 4

## File Classification

| 신규/수정 파일 | 역할 | 데이터 흐름 | 가장 가까운 아날로그 | 일치 품질 |
|---|---|---|---|---|
| `WinFormsApp1/Logic/EventWaypointBoxPropagationHelper.cs` | utility | transform | `WinFormsApp1/Logic/EventWaypointUpdateHelper.cs` | 역할 일치 |
| `WinFormsApp1/Forms/Form1.EventPropagation.cs` | controller | event-driven | 같은 파일의 `PropagateEventBoxFromCurrentFrame` | 정확 |
| `WinFormsApp1/Forms/Form1.Drawing.cs` | controller | event-driven | 같은 파일의 drag/resize 완료 및 삭제 처리 | 정확 |
| `WinFormsApp1.Tests/Program.cs` | test | transform | 같은 파일의 event-waypoint helper 회귀 테스트 | 정확 |

## Pattern Assignments

### `WinFormsApp1/Logic/EventWaypointBoxPropagationHelper.cs` (utility, transform)

**아날로그:** `WinFormsApp1/Logic/EventWaypointUpdateHelper.cs`

이 프로젝트의 순수 로직 helper는 `WinFormsApp1` 네임스페이스 아래 정적 클래스로 두며, `IEnumerable` 입력을 받아 변경 가능한 결과/목록을 반환한다. UI 상태나 `Form1`을 참조하지 않는다.

**Imports 및 public static helper 패턴** (`WinFormsApp1/Logic/EventWaypointUpdateHelper.cs:1-30`):

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace WinFormsApp1
{
    public static class EventWaypointUpdateHelper
    {
```

**instance-first, fail-closed 범위 해석** (`WinFormsApp1/Logic/EventWaypointUpdateHelper.cs:62-119`):

```csharp
var eventWaypoints = waypoints
    .Where(waypoint => waypoint != null &&
        string.Equals(waypoint.Label, "event", StringComparison.OrdinalIgnoreCase) &&
        selectedBox.FrameIndex >= waypoint.EntryFrame &&
        selectedBox.FrameIndex <= waypoint.ExitFrame)
    .ToList();

if (!string.IsNullOrWhiteSpace(selectedBox.EventInstanceId))
{
    var instanceWaypoints = eventWaypoints
        .Where(waypoint => string.Equals(
            waypoint.EventInstanceId,
            selectedBox.EventInstanceId,
            StringComparison.Ordinal))
        .ToList();

    if (instanceWaypoints.Count != 1)
    {
        var mixedWaypoints = eventWaypoints
            .Where(waypoint => string.IsNullOrWhiteSpace(waypoint.EventInstanceId) &&
                waypoint.ObjectId == selectedBox.EventId)
            .ToList();

        if (mixedWaypoints.Count != 1)
            return null;
    }
}
```

**활성 box 필터링 패턴** (`WinFormsApp1/Logic/EventWaypointUpdateHelper.cs:148-160`):

```csharp
return boxes
    .Where(box => box != null &&
        !box.IsDeleted &&
        string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) &&
        box.FrameIndex >= waypoint.EntryFrame &&
        box.FrameIndex <= waypoint.ExitFrame &&
        matchesWaypoint(box))
    .ToList();
```

**적용:** 새 helper는 `ResolveActiveScope` 결과를 먼저 받아야 한다. 그 결과의 waypoint 범위에서 `source.FrameIndex + 1`부터 `ExitFrame`까지 처리하고, 매 frame마다 (1) 삭제 tombstone, (2) 수동 보정 frame, (3) 활성 derived box, (4) box 부재를 구분한다. 범위가 없거나 모호하면 빈 변경 결과를 반환한다. `EventId`나 사각형만으로 범위를 선택하지 않는다.

**이벤트 identity key 재사용** (`WinFormsApp1/Logic/TrackingIdentityHelper.cs:69-76`, `WinFormsApp1/Forms/Form1.Helpers.cs:222-225`):

```csharp
public static string GetIdentityKey(BoundingBox box)
{
    if (box == null) return "unknown_unknown";
    if (string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(box.EventInstanceId))
        return $"event_instance_{box.EventInstanceId}";
    return $"{box.Label}_{GetNumericIdentity(box)}";
}

private string GetDrawingIdentityKey(BoundingBox box)
{
    return TrackingIdentityHelper.GetIdentityKey(box);
}
```

### `WinFormsApp1/Forms/Form1.EventPropagation.cs` (controller, event-driven)

**아날로그:** 같은 파일의 `PropagateEventBoxFromCurrentFrame` (`:428-515`)

기존 메서드는 바로 이 phase의 연결점이지만 현재는 manual-tracking-first guard로 event 변경을 조기 반환한다. guard만 제거하는 것으로 끝내지 말고, 새 순수 helper의 결과를 적용하는 어댑터로 교체한다.

**기존 호출 계약 및 범위 계산** (`WinFormsApp1/Forms/Form1.EventPropagation.cs:428-456`):

```csharp
private void PropagateEventBoxFromCurrentFrame(BoundingBox box)
{
    if (box?.Label == "event")
    {
        System.Diagnostics.Debug.WriteLine(
            $"[Event Lifetime Guard] Manual-tracking-first mode: PropagateEventBoxFromCurrentFrame skipped for instance={box.EventInstanceId}, frame={box.FrameIndex}");
        return;
    }
    if (box.Label != "event")
        return;

    int startFrame = box.FrameIndex + 1;
    int endFrame = waypoint.ExitFrame;
    if (box.FrameIndex >= endFrame)
        return;
}
```

**기존 복제 객체 초기화 패턴** (`WinFormsApp1/Forms/Form1.EventPropagation.cs:486-497`):

```csharp
var newBox = new BoundingBox
{
    Rectangle = box.Rectangle,
    Label = box.Label,
    FrameIndex = frame,
    PersonId = box.PersonId,
    VehicleId = box.VehicleId,
    EventId = box.EventId,
    EventInstanceId = box.EventInstanceId,
    Action = "waypoint"
};
boundingBoxes.Add(newBox);
```

**변경 후 캐시/목록 갱신 패턴** (`WinFormsApp1/Forms/Form1.EventPropagation.cs:503-509`):

```csharp
if (updatedCount > 0)
{
    InvalidateBoxCache();
    UpdateBoxCount();
    UpdateBboxListDisplay();
}
```

**적용:** helper가 준 update/create만 `boundingBoxes`에 적용한다. 삭제된 동일-instance box는 새 box로 대체하지 않으며, 수동 보정으로 기록된 이후 frame의 rectangle도 갱신하지 않는다. 생성은 개별 frame에 동일 instance의 box 자체가 없을 때만 수행한다. 이 메서드는 tracked-object 변경 경로에서 호출하지 않는다(Phase 7 범위).

### `WinFormsApp1/Forms/Form1.Drawing.cs` (controller, event-driven)

**아날로그:** 같은 파일의 resize/drag 완료 경로 (`:414-456`) 및 단일 box 삭제 경로 (`:2752-2769`)

**수동 resize/drag 완료 후 호출 패턴** (`WinFormsApp1/Forms/Form1.Drawing.cs:419-435`, `:449-457`):

```csharp
var undoBox = CloneBoundingBox(selectedBox);
undoBox.Rectangle = originalResizeRect;
AddUndoAction(new UndoAction { Type = UndoActionType.ModifyBox, Box = undoBox });

RecordManuallyAdjustedFrame(selectedBox);

InvalidateBoxCache();
UpdateObjectInfo(selectedBox);
UpdateBboxListDisplay();

if (selectedBox != null && selectedBox.Label == "event")
{
    PropagateEventBoxFromCurrentFrame(selectedBox);
}
```

**수동 보정 provenance 기록 패턴** (`WinFormsApp1/Forms/Form1.Drawing.cs:2848-2868`):

```csharp
private void RecordManuallyAdjustedFrame(BoundingBox box)
{
    if (box == null) return;

    var waypoint = FindWaypointForBox(box);
    if (waypoint == null) return;

    string key = GetDrawingIdentityKey(box);
    if (!manuallyAdjustedFrames.ContainsKey(key))
    {
        manuallyAdjustedFrames[key] = new List<int>();
    }

    if (!manuallyAdjustedFrames[key].Contains(box.FrameIndex))
    {
        manuallyAdjustedFrames[key].Add(box.FrameIndex);
        manuallyAdjustedFrames[key].Sort();
    }
}
```

**단일 frame 삭제(tombstone) 패턴** (`WinFormsApp1/Forms/Form1.Drawing.cs:2752-2769`):

```csharp
AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(selectedBox) });
selectedBox.IsDeleted = true;

selectedBox = null;
ClearSidebarHighlights();
UpdateBoxCount();
UpdateBboxListDisplay();
pictureBoxVideo.Invalidate();
```

**즉시 UI 갱신 추가 위치:** 위 삭제 경로와 event edit 전파 완료 뒤에 `UpdateEventListDisplay()` 및 `UpdateWaypointListView()`를 호출하고 마지막에 `pictureBoxVideo.Invalidate()` 한다. 이벤트 목록은 현재 frame의 활성 event만 보며(`WinFormsApp1/Forms/Form1.Drawing.cs:2615-2617`), waypoint 행은 `EventWaypointUpdateHelper.FindDisplayBox`로 instance-aware box를 고른다(`:1584-1588`).

```csharp
var currentBoxes = boundingBoxes
    .Where(b => b.FrameIndex == currentFrameIndex && b.Label == "event" && !b.IsDeleted)
    .ToList();

var eventBox = EventWaypointUpdateHelper.FindDisplayBox(boundingBoxes, waypoint);
```

**삭제 범위 구분:** 단일 box 삭제는 tombstone만 남긴다. waypoint 삭제의 전체 범위 제거는 기존 `EventFinalizationHelper.GetEventBoxesForWaypoint`을 계속 사용한다(`WinFormsApp1/Forms/Form1.Timeline.cs:692-725`).

### `WinFormsApp1.Tests/Program.cs` (test, transform)

**아날로그:** 같은 파일의 `(Name, Action)` console harness 및 event-waypoint scope 테스트

**테스트 등록/실행 패턴** (`WinFormsApp1.Tests/Program.cs:7-75`):

```csharp
var tests = new (string Name, Action Run)[]
{
    ("event type update resolves active boxes by event instance", EventTypeUpdateResolvesActiveBoxesByEventInstance),
    ("event type update rejects ambiguous mixed markers", EventTypeUpdateRejectsAmbiguousMixedMarkers),
};

foreach (var (name, run) in tests)
{
    run();
    Console.WriteLine($"PASS {name}");
}
```

**동일 instance / deleted / 다른 instance fixture 패턴** (`WinFormsApp1.Tests/Program.cs:203-244`):

```csharp
var sameWaypointDifferentRectangle = new BoundingBox
{
    Label = "event", EventId = 1, EventInstanceId = "event-a",
    FrameIndex = 13, Rectangle = new Rectangle(20, 20, 30, 30)
};
var deleted = new BoundingBox
{
    Label = "event", EventId = 1, EventInstanceId = "event-a",
    FrameIndex = 14, IsDeleted = true
};
var otherWaypoint = new BoundingBox
{
    Label = "event", EventId = 1, EventInstanceId = "event-b", FrameIndex = 12
};

AssertTrue(!resolved.Contains(deleted), "Deleted event boxes must remain unchanged.");
AssertTrue(!resolved.Contains(otherWaypoint), "Same-type boxes in another event instance must remain unchanged.");
```

**모호한 scope fail-closed 테스트 패턴** (`WinFormsApp1.Tests/Program.cs:307-315`):

```csharp
var scope = EventWaypointUpdateHelper.ResolveActiveScope(selected, new[] { selected }, new[] { first, second });

AssertTrue(scope == null, "Ambiguous blank-instance markers must fail closed.");
```

**적용:** 새 helper의 순수 단위 테스트를 이 파일에 등록한다. 최소한 다음을 검증한다: 현재 frame 이전 미변경, exit까지 update/create, 뒤쪽 manual frame 보호, deleted tombstone 재생성 금지와 뒤쪽 box 보존, 다른 `EventInstanceId` 격리, 모호한 scope 무변경. UI refresh는 Windows Forms 수동 smoke로 확인한다.

## Shared Patterns

### Event identity 및 범위 해석

**출처:** `WinFormsApp1/Logic/EventWaypointUpdateHelper.cs:62-119`, `WinFormsApp1/Logic/TrackingIdentityHelper.cs:85-90`  
**적용 대상:** 새 propagation helper와 `Form1.EventPropagation.cs`

```csharp
if (string.Equals(box.Label, "event", StringComparison.OrdinalIgnoreCase) &&
    !string.IsNullOrWhiteSpace(box.EventInstanceId) &&
    !string.IsNullOrWhiteSpace(waypoint.EventInstanceId))
    return string.Equals(box.EventInstanceId, waypoint.EventInstanceId, StringComparison.Ordinal);
return GetNumericIdentity(box) == waypoint.ObjectId;
```

`ResolveActiveScope`가 null이면 변경하지 않는다. legacy fallback은 둘 다 instance ID가 없는 경우에만 허용한다.

### 삭제와 수동 보정의 분리

**출처:** `WinFormsApp1/Models/Annotation/BoundingBox.cs:8-25`, `WinFormsApp1/Forms/Form1.Drawing.cs:2848-2895`  
**적용 대상:** propagation helper, edit/delete UI 경로

```csharp
public bool IsDeleted { get; set; }

if (!manuallyAdjustedFrames[key].Contains(box.FrameIndex))
{
    manuallyAdjustedFrames[key].Add(box.FrameIndex);
    manuallyAdjustedFrames[key].Sort();
}
```

`IsDeleted`는 해당 frame의 삭제 tombstone이고, `manuallyAdjustedFrames`는 이후 propagation이 덮어쓰지 말아야 할 별도의 provenance다. 새 JSON 필드는 추가하지 않는다.

### UI 갱신 순서

**출처:** `WinFormsApp1/Forms/Form1.EventPropagation.cs:503-509`, `WinFormsApp1/Forms/Form1.Drawing.cs:1541-1545`, `:2602-2617`  
**적용 대상:** event edit와 단일 event delete 경로

변경 반영 후 `InvalidateBoxCache()` → `UpdateBoxCount()` → `UpdateBboxListDisplay()` → `UpdateEventListDisplay()` → `UpdateWaypointListView()` → `pictureBoxVideo.Invalidate()` 순서를 사용한다. timeline 표식이 변한 경우에만 기존처럼 `panelTimeline.Invalidate()`를 추가한다.

### Waypoint 전체 삭제는 기존 helper 유지

**출처:** `WinFormsApp1/Forms/Form1.Timeline.cs:692-725`  
**적용 대상:** waypoint 삭제 경로만

```csharp
boxesToDelete = EventFinalizationHelper.GetEventBoxesForWaypoint(
    boundingBoxes,
    waypoint)
    .ToList();

foreach (var box in boxesToDelete)
{
    AddUndoAction(new UndoAction { Type = UndoActionType.RemoveBox, Box = CloneBoundingBox(box) });
    boundingBoxes.Remove(box);
}
```

## No Analog Found

| 파일 | 역할 | 데이터 흐름 | 사유 |
|---|---|---|---|
| 없음 | - | - | 새 helper의 정확한 전파 상태 전이 자체는 없지만, scope/identity/삭제/테스트의 구성 요소 아날로그가 모두 존재한다. |

## Metadata

**아날로그 검색 범위:** `WinFormsApp1/Forms`, `WinFormsApp1/Logic`, `WinFormsApp1/Models/Annotation`, `WinFormsApp1.Tests`  
**스캔 파일 수:** 9  
**패턴 추출일:** 2026-07-23
