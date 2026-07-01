# Manual Labeling UI Expansion Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Person body/face, vehicle/plate를 모두 박스 단위 entry/exit와 자동 parent 매칭이 가능한 구조로 확장하고, Event 확장 및 Camouflage 속성, 겹침 친화적 UI까지 반영한다.

**Architecture:** 기존 단일 WinForms 구조를 유지하되 `BoundingBox`에 하위 객체 메타데이터와 box 단위 구간 정보를 추가한다. body/vehicle이 상위 객체이며, face/plate는 각자 독립적인 box entry/exit를 가지되 이미 존재하는 상위 waypoint에 종속된다. 하위 box의 parent는 해당 entry/exit 구간 내 body/vehicle 후보 중 좌표가 가장 가까운 bbox의 ID로 결정하고, entry/exit 확정 후 자동 관성 추적을 실행한다.

**Tech Stack:** .NET 8 WinForms, C#, Newtonsoft.Json, OpenCvSharp, 단일 메인 화면 로직 `WinFormsApp1/Form1.cs`, 속성 팝업 `WinFormsApp1/PersonAttributesForm.cs`

---

### Task 1: 현재 브랜치 기준선 확인 및 하위 객체 데이터 모델 재정의

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Test: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: 현재 Event 저장 안정성 브랜치가 깨지지 않았는지 빌드 확인**

Run:
```powershell
dotnet build ETRILabelingTool.sln
```

Expected:
```text
Build succeeded.
```

- [ ] **Step 2: `BoundingBox`에 하위 객체용 필드 추가**

추가 필드:
```csharp
public string PersonPartType { get; set; } // "body" | "face" | null
public int? FaceId { get; set; }
public int? LinkedPersonId { get; set; }
public string VehicleSubPart { get; set; } // "plate" | null
public int? LinkedVehicleId { get; set; }
public int? BoxEntryFrame { get; set; }
public int? BoxExitFrame { get; set; }
```

- [ ] **Step 3: `CloneBoundingBox`에 새 필드 복사 추가**

예시:
```csharp
PersonPartType = box.PersonPartType,
FaceId = box.FaceId,
LinkedPersonId = box.LinkedPersonId,
VehicleSubPart = box.VehicleSubPart,
LinkedVehicleId = box.LinkedVehicleId,
BoxEntryFrame = box.BoxEntryFrame,
BoxExitFrame = box.BoxExitFrame,
```

- [ ] **Step 4: Event 타입 메타데이터를 7종으로 확장**

수정 대상:
- `EventTypes`
- `CategoryIdMap`
- `GetCategoryName`
- `GetCategoryId`

추가 Event:
```csharp
"disembark",
"controlled_delivery",
"camouflage"
```

- [ ] **Step 5: parent 매칭용 기초 헬퍼 추가**

```csharp
private int CalculateOverlapArea(Rectangle a, Rectangle b) { ... }
private double CalculateCenterDistance(Rectangle a, Rectangle b) { ... }
```

- [ ] **Step 6: 빌드 재확인**

Run:
```powershell
dotnet build ETRILabelingTool.sln
```

Expected:
```text
Build succeeded.
```

### Task 2: Draw/Select 모드에서 body/face 입력 분기와 face_id 단축키 확정

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Test: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: 입력 상태 필드 추가**

```csharp
private int currentAssignedId = 1;
private int currentFaceAssignedId = 1;
private MouseButtons? currentDrawMouseButton = null;
```

- [ ] **Step 2: Draw 모드 마우스 버튼 분기 적용**

규칙:
- Person + 좌드래그 = body
- Person + 우드래그 = face
- Select 모드 우클릭 = 기존 Person 속성 팝업 유지

- [ ] **Step 3: `drawingBox` 생성 시 body/face 메타데이터 채우기**

```csharp
bool isPersonBody = currentSelectedLabel == "person" && currentDrawMouseButton == MouseButtons.Left;
bool isPersonFace = currentSelectedLabel == "person" && currentDrawMouseButton == MouseButtons.Right;

PersonPartType = isPersonBody ? "body" : isPersonFace ? "face" : null,
FaceId = isPersonFace ? currentFaceAssignedId : null,
LinkedPersonId = null,
BoxEntryFrame = currentFrameIndex,
BoxExitFrame = currentFrameIndex,
```

- [ ] **Step 4: face box 선택 시 `Ctrl/Alt` 단축키로 face_id 할당 유지**

규칙:
- 선택된 box가 `person + face`일 때만 face_id 단축키 적용
- `Ctrl+1~0` = 1~10
- `Alt+1~0` = 11~20

- [ ] **Step 5: `GetBoxLabelText`를 body/face 표기로 변경**

```csharp
if (box.PersonPartType == "face")
    return $"person_face_{box.FaceId.GetValueOrDefault():D2}->body_{(box.LinkedPersonId.HasValue ? box.LinkedPersonId.Value.ToString("D2") : "--")}";
return $"person_body_{box.PersonId:D2}";
```

- [ ] **Step 6: face는 YOLO 자동 추적 대상에서 제외**

```csharp
var trackableBoxes = currentFrameBoxes
    .Where(b => !(b.Label == "person" && b.PersonPartType == "face"))
    .ToList();
```

- [ ] **Step 7: 빌드 재확인**

Run:
```powershell
dotnet build ETRILabelingTool.sln
```

Expected:
```text
Build succeeded.
```

### Task 3: box 단위 entry/exit 모델 추가 및 하위 box 구간 확정 흐름 구현

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Test: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: 하위 box용 임시 entry/exit 상태 변수 추가**

예시:
```csharp
private BoundingBox pendingSubPartBox = null;
private int? subPartEntryFrameIndex = null;
```

- [ ] **Step 2: 하위 box 생성 직후 entry 기본값을 현재 프레임으로 설정**

```csharp
if (drawingBox.PersonPartType == "face" || drawingBox.VehicleSubPart == "plate")
{
    drawingBox.BoxEntryFrame = currentFrameIndex;
    drawingBox.BoxExitFrame = currentFrameIndex;
}
```

- [ ] **Step 3: 하위 box의 exit 확정 UI/동작 경로 추가**

원칙:
- 하위 box도 독립적인 `BoxEntryFrame` / `BoxExitFrame`을 가져야 함
- 단, parent waypoint를 새로 만드는 것이 아니라 기존 body/vehicle waypoint 내부 구간이어야 함

최소 구현:
- 선택된 face/plate box에 대해 현재 프레임을 `BoxExitFrame`으로 확정하는 경로 추가
- `BoxExitFrame >= BoxEntryFrame` 검증

- [ ] **Step 4: 하위 box의 entry/exit 구간이 속한 parent waypoint 찾기**

```csharp
private WaypointMarker FindParentWaypointForSubPart(BoundingBox subPartBox)
{
    // person face -> person body waypoint
    // vehicle plate -> vehicle waypoint
}
```

규칙:
- subPart box의 entry/exit 전체를 포함하는 waypoint 후보만 본다
- 후보가 여러 개면 다음 단계 거리 기반 선택으로 parent id 결정

### Task 4: parent ID 자동 결정 규칙 구현 (겹침 + 거리 기반)

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Test: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: subPart box 구간 내 parent 후보 수집 함수 추가**

```csharp
private List<BoundingBox> FindParentCandidatesForSubPart(BoundingBox subPartBox, string parentLabel)
{
    // subPartBox.BoxEntryFrame~BoxExitFrame 범위 내 후보 수집
}
```

- [ ] **Step 2: 가장 가까운 parent bbox를 고르는 함수 추가**

```csharp
private BoundingBox FindNearestParentBoxForSubPart(BoundingBox subPartBox, IEnumerable<BoundingBox> candidates)
{
    // 겹침 면적 우선
    // 겹침이 없거나 동률이면 중심점 거리 최소
}
```

- [ ] **Step 3: face의 LinkedPersonId 결정**

규칙:
- subPart의 entry/exit가 속한 waypoint 안의 body 후보 중 가장 가까운 body bbox의 `PersonId`
- waypoint가 여러 개면 후보 전체를 비교하되 가장 가까운 body bbox 하나로 결정

- [ ] **Step 4: plate의 LinkedVehicleId 결정**

규칙:
- subPart의 entry/exit가 속한 vehicle waypoint 안의 vehicle bbox 후보 중 가장 가까운 vehicle bbox의 `VehicleId`

- [ ] **Step 5: 결정된 parent ID를 라벨 텍스트와 object info에 반영**

### Task 5: 하위 box entry/exit 확정 후 자동 관성 추적 연결

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Test: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: face/plate entry-exit 확정 후 자동 관성 추적 진입점 추가**

원칙:
- body/vehicle처럼 YOLO 추적이 아니라 기존 관성 추적 또는 보간 계열 수동 추적 사용
- face는 YOLO 대상 아님

- [ ] **Step 2: face box 구간에 대해 자동 복제/보간 실행**

예시 개념:
```csharp
private void PropagateSubPartBoxWithinRange(BoundingBox subPartBox, int endFrame)
{
    // BoxEntryFrame~BoxExitFrame 사이에 face/plate box 생성
    // parent linkage 유지
}
```

- [ ] **Step 3: 생성된 모든 하위 box에 parent linkage 복사**

```csharp
newBox.FaceId = subPartBox.FaceId;
newBox.LinkedPersonId = subPartBox.LinkedPersonId;
newBox.VehicleSubPart = subPartBox.VehicleSubPart;
newBox.LinkedVehicleId = subPartBox.LinkedVehicleId;
newBox.BoxEntryFrame = subPartBox.BoxEntryFrame;
newBox.BoxExitFrame = subPartBox.BoxExitFrame;
```

### Task 6: Vehicle plate 흐름과 Event/Camouflage/UI 보완

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Modify: `WinFormsApp1/PersonAttributesForm.cs`
- Test: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: Vehicle plate 입력 경로를 face와 같은 방식으로 분기**

plate도 별도 box, 별도 `BoxEntryFrame/BoxExitFrame`, `LinkedVehicleId`를 가진다.

- [ ] **Step 2: Event 종류 UI를 7종으로 정리**

수정 대상:
- `UpdateEventListDisplay`
- event combo items
- 이름 역매핑 함수
- 종료 다이얼로그 배열

- [ ] **Step 3: PersonAttributesForm에 Camouflage 추가**

```csharp
("Camouflage", new[] { "None", "Camouflaged" })
```

표시값:
```csharp
{ "Camouflage", "변장" },
{ "None", "없음" },
{ "Camouflaged", "변장 상태" }
```

- [ ] **Step 4: BBOX 선 굵기 조정**

```csharp
using (Pen pen = new Pen(boxColor, 2))
```

선택 강조:
```csharp
using (Pen pen = new Pen(boxColor, 2.5f))
```

### Task 7: JSON 저장 구조 반영 및 회귀 검증

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Modify: `WinFormsApp1/PersonAttributesForm.cs` (필요 시)
- Test: `dotnet build ETRILabelingTool.sln`
- Run: `dotnet run --project WinFormsApp1/WinFormsApp1.csproj`

- [ ] **Step 1: body/face/plate를 옵션 3 구조로 직렬화**

```csharp
annotation.Attributes ??= new Dictionary<string, object>();

if (box.Label == "person")
{
    annotation.Attributes["person_part_type"] = box.PersonPartType ?? "body";
    if (box.PersonPartType == "face")
    {
        annotation.Attributes["face_id"] = box.FaceId;
        annotation.Attributes["linked_person_id"] = box.LinkedPersonId;
    }
}

if (box.Label == "vehicle" && box.VehicleSubPart == "plate")
{
    annotation.Attributes["vehicle_sub_part"] = "plate";
    annotation.Attributes["linked_vehicle_id"] = box.LinkedVehicleId;
}
```

- [ ] **Step 2: 하위 box의 box-level entry/exit도 저장**

```csharp
annotation.Attributes["box_entry_frame"] = box.BoxEntryFrame;
annotation.Attributes["box_exit_frame"] = box.BoxExitFrame;
```

- [ ] **Step 3: JSON 로드 시 하위 box 필드 복원**

복원 대상:
- `PersonPartType`
- `FaceId`
- `LinkedPersonId`
- `VehicleSubPart`
- `LinkedVehicleId`
- `BoxEntryFrame`
- `BoxExitFrame`

- [ ] **Step 4: 빌드 실행**

Run:
```powershell
dotnet build ETRILabelingTool.sln
```

Expected:
```text
Build succeeded.
```

- [ ] **Step 5: 앱 실행 후 수동 검증**

Run:
```powershell
dotnet run --project WinFormsApp1/WinFormsApp1.csproj
```

수동 검증 체크리스트:
- Person body는 기존처럼 waypoint를 만든다.
- face box는 자체 entry/exit를 가진다.
- face box의 entry/exit가 속한 구간에서 가장 가까운 body bbox의 `person_id`가 연결된다.
- 후보 waypoint/body가 여러 개면 가장 가까운 body 기준으로 연결된다.
- face entry/exit 확정 시 자동 관성 추적이 돈다.
- vehicle plate도 동일 규칙으로 연결/추적된다.
- Select 모드 우클릭은 여전히 속성 팝업을 연다.
- face는 YOLO 추적 대상이 아니다.
- Camouflage 속성이 팝업에 보이고 저장된다.
- Event 7종이 정상 표시된다.
- 저장 후 재로드해도 Event 저장 안정성이 깨지지 않는다.
- 저장 후 재로드해도 face/plate linkage와 box entry/exit가 유지된다.

- [ ] **Step 6: Commit**

```bash
git add WinFormsApp1/Form1.cs WinFormsApp1/PersonAttributesForm.cs
git commit -m "feat: add subordinate face and plate labeling workflow"
```