# JSON/Model Separation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `Form1.cs`에 섞여 있는 모델/JSON 타입 선언을 `Models` 폴더로 이동해 화면 로직과 데이터 정의의 경계를 분리한다.

**Architecture:** `Form1`의 동작 로직은 유지하고, 타입 정의만 `Models/Annotation`과 `Models/Json`으로 재배치한다. 첫 단계에서는 메서드 분리나 서비스 도입 없이 namespace/using 정리와 빌드 검증만 수행해 리스크를 최소화한다.

**Tech Stack:** .NET 8 WinForms, C#, Newtonsoft.Json, 단일 메인 폼 구조, subordinate face/plate 메타데이터 포함 JSON 포맷

---

## File Structure

### Create

- `WinFormsApp1/Models/Annotation/BoundingBox.cs`
- `WinFormsApp1/Models/Annotation/WaypointMarker.cs`
- `WinFormsApp1/Models/Annotation/CustomLabel.cs`
- `WinFormsApp1/Models/Annotation/SubtitleEntry.cs`
- `WinFormsApp1/Models/Json/LabelingDataModels.cs`
- `WinFormsApp1/Models/Json/LegacyJsonModels.cs`

### Modify

- `WinFormsApp1/Form1.cs`
- `WinFormsApp1/WinFormsApp1.csproj` (필요 시 명시적 Compile Include 정리)

### Verify

- `dotnet build ETRILabelingTool.sln`
- `dotnet run --project WinFormsApp1/WinFormsApp1.csproj`

---

### Task 1: 현재 타입 선언 범위 고정 및 이동 대상 확정

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Verify: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: 타입 선언 위치를 재확인한다**

확인 대상:
- `WaypointMarker`
- `CustomLabel`
- `BoundingBox`
- `SubtitleEntry`
- `ImageInfo`
- `TrackEntry`
- `TrackInfo`
- `AnnotationData`
- `CategoryData`
- `VideoInfoExtended`
- `LabelingDataExtended`
- `Legacy JSON Classes` region

Run:
```powershell
Select-String -Path 'WinFormsApp1\Form1.cs' -Pattern 'public class WaypointMarker|public class CustomLabel|public class BoundingBox|public class SubtitleEntry|public class ImageInfo|public class TrackEntry|public class TrackInfo|public class AnnotationData|public class CategoryData|public class VideoInfoExtended|public class LabelingDataExtended|#region Legacy JSON Classes'
```

Expected:
```text
모든 대상 타입이 Form1.cs에 존재함
```

- [ ] **Step 2: 기준선 빌드를 확인한다**

Run:
```powershell
dotnet build ETRILabelingTool.sln
```

Expected:
```text
Build succeeded.
```

---

### Task 2: Annotation 모델 파일 생성

**Files:**
- Create: `WinFormsApp1/Models/Annotation/WaypointMarker.cs`
- Create: `WinFormsApp1/Models/Annotation/BoundingBox.cs`
- Create: `WinFormsApp1/Models/Annotation/CustomLabel.cs`
- Create: `WinFormsApp1/Models/Annotation/SubtitleEntry.cs`
- Modify: `WinFormsApp1/Form1.cs`
- Verify: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: `WaypointMarker` 파일을 만든다**

```csharp
using System.Drawing;

namespace WinFormsApp1
{
    public class WaypointMarker
    {
        public int EntryFrame { get; set; }
        public int ExitFrame { get; set; }
        public Color MarkerColor { get; set; }
        public string EntryTime { get; set; }
        public string ExitTime { get; set; }
        public int ObjectId { get; set; }
        public string EventInstanceId { get; set; }
        public string Label { get; set; }
        public string InteractingObject { get; set; }
    }
}
```

- [ ] **Step 2: `BoundingBox` 파일을 만든다**

포함 필드:
- `FrameIndex`
- `Rectangle`
- `Label`
- `PersonId`
- `VehicleId`
- `EventId`
- `Action`
- `EventInstanceId`
- `VehicleName`
- `EventName`
- `PersonPartType`
- `FaceId`
- `LinkedPersonId`
- `VehicleSubPart`
- `LinkedVehicleId`
- `BoxEntryFrame`
- `BoxExitFrame`
- `IsDeleted`
- `PersonAttributes`
- `Skeleton3D`

```csharp
using System.Collections.Generic;
using System.Drawing;

namespace WinFormsApp1
{
    public class BoundingBox
    {
        public int FrameIndex { get; set; }
        public Rectangle Rectangle { get; set; }
        public string Label { get; set; }
        public int PersonId { get; set; }
        public int VehicleId { get; set; }
        public int EventId { get; set; }
        public string Action { get; set; }
        public string EventInstanceId { get; set; }
        public string VehicleName { get; set; }
        public string EventName { get; set; }
        public string PersonPartType { get; set; }
        public int? FaceId { get; set; }
        public int? LinkedPersonId { get; set; }
        public string VehicleSubPart { get; set; }
        public int? LinkedVehicleId { get; set; }
        public int? BoxEntryFrame { get; set; }
        public int? BoxExitFrame { get; set; }
        public bool IsDeleted { get; set; }
        public Dictionary<string, object> PersonAttributes { get; set; }
        public List<List<double>> Skeleton3D { get; set; }
    }
}
```

- [ ] **Step 3: `CustomLabel` 파일을 만든다**

```csharp
using System.Windows.Forms;

namespace WinFormsApp1
{
    public class CustomLabel
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int Id { get; set; }
        public Panel Panel { get; set; }
        public Label Label { get; set; }
    }
}
```

- [ ] **Step 4: `SubtitleEntry` 파일을 만든다**

```csharp
using System;

namespace WinFormsApp1
{
    public class SubtitleEntry
    {
        public int Index { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string Text { get; set; }
    }
}
```

- [ ] **Step 5: `Form1.cs`에서 위 네 타입 선언을 제거한다**

제거 대상 범위:
- `WaypointMarker`
- `CustomLabel`
- `BoundingBox`
- `SubtitleEntry`

주의:
- 로직은 절대 수정하지 않는다.
- namespace는 그대로 `WinFormsApp1`을 사용한다.

- [ ] **Step 6: 빌드를 실행해 타입 이동만으로 깨진 참조가 없는지 확인한다**

Run:
```powershell
dotnet build ETRILabelingTool.sln
```

Expected:
```text
Build succeeded.
```

---

### Task 3: JSON 모델 파일 생성

**Files:**
- Create: `WinFormsApp1/Models/Json/LabelingDataModels.cs`
- Modify: `WinFormsApp1/Form1.cs`
- Verify: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: `LabelingDataModels.cs` 파일을 만든다**

포함 타입:
- `ImageInfo`
- `TrackEntry`
- `TrackInfo`
- `AnnotationData`
- `CategoryData`
- `VideoInfoExtended`
- `LabelingDataExtended`

필수 조건:
- 기존 `[JsonProperty(...)]` 속성을 그대로 유지한다.
- `event_instance_id`, `person_attributes`, `attributes`, `skeleton_3d`, `keypoints_3d` 필드 정의를 그대로 유지한다.

- [ ] **Step 2: `Form1.cs`에서 JSON serialization class 블록을 제거한다**

제거 대상:
- `#region JSON Serialization Classes` 내부 타입 선언 전체

주의:
- 메서드 본문은 수정하지 않는다.
- JSON 속성 이름은 한 글자도 바꾸지 않는다.

- [ ] **Step 3: 빌드로 JSON 참조 무결성을 확인한다**

Run:
```powershell
dotnet build ETRILabelingTool.sln
```

Expected:
```text
Build succeeded.
```

---

### Task 4: Legacy JSON 호환 타입 분리

**Files:**
- Create: `WinFormsApp1/Models/Json/LegacyJsonModels.cs`
- Modify: `WinFormsApp1/Form1.cs`
- Verify: `dotnet build ETRILabelingTool.sln`

- [ ] **Step 1: `LegacyJsonModels.cs` 파일을 만든다**

포함 대상:
- 현재 `#region Legacy JSON Classes (호환성 유지)` 안의 타입들 전체

조건:
- 타입명과 프로퍼티명을 바꾸지 않는다.
- JSON 호환성 유지 목적이므로 구조 정리는 하지 않는다.

- [ ] **Step 2: `Form1.cs`에서 legacy JSON 타입 선언을 제거한다**

제거 대상:
- `#region Legacy JSON Classes (호환성 유지)` 전체

- [ ] **Step 3: 빌드로 deserialize 경로가 그대로 유지되는지 확인한다**

Run:
```powershell
dotnet build ETRILabelingTool.sln
```

Expected:
```text
Build succeeded.
```

---

### Task 5: using/참조 정리 및 최소 실행 검증

**Files:**
- Modify: `WinFormsApp1/Form1.cs`
- Modify: `WinFormsApp1/WinFormsApp1.csproj` (필요 시)
- Verify: `dotnet build ETRILabelingTool.sln`
- Verify: `dotnet run --project WinFormsApp1/WinFormsApp1.csproj`

- [ ] **Step 1: `Form1.cs` 상단 using을 정리한다**

확인 항목:
- `System`
- `System.Collections.Generic`
- `System.Drawing`
- `System.Windows.Forms`
- `Newtonsoft.Json`

원칙:
- 새 모델이 같은 namespace면 과도한 using 추가는 하지 않는다.
- 제거 가능한 using만 정리한다.

- [ ] **Step 2: 프로젝트 파일 수정이 필요한지 확인한다**

Run:
```powershell
Select-String -Path 'WinFormsApp1\WinFormsApp1.csproj' -Pattern '<Compile Include='
```

Expected:
```text
SDK-style project이면 추가 Compile Include 없이 새 cs 파일이 자동 포함됨
```

- [ ] **Step 3: 전체 빌드를 실행한다**

Run:
```powershell
dotnet build ETRILabelingTool.sln
```

Expected:
```text
Build succeeded.
```

- [ ] **Step 4: 앱을 실행해 초기 크래시가 없는지 확인한다**

Run:
```powershell
dotnet run --project WinFormsApp1/WinFormsApp1.csproj
```

Expected:
```text
앱이 시작되고 즉시 타입 로드 예외 없이 실행됨
```

- [ ] **Step 5: 변경 파일만 커밋한다**

```bash
git add WinFormsApp1/Form1.cs WinFormsApp1/Models/Annotation/BoundingBox.cs WinFormsApp1/Models/Annotation/WaypointMarker.cs WinFormsApp1/Models/Annotation/CustomLabel.cs WinFormsApp1/Models/Annotation/SubtitleEntry.cs WinFormsApp1/Models/Json/LabelingDataModels.cs WinFormsApp1/Models/Json/LegacyJsonModels.cs docs/superpowers/specs/2026-07-01-json-model-separation-design.md docs/superpowers/plans/2026-07-01-json-model-separation-implementation.md
git commit -m "refactor: separate json and annotation models"
```

---

## Self-Review

### Spec coverage

- `BoundingBox`, `WaypointMarker`, `CustomLabel`, `SubtitleEntry` 분리: Task 2
- JSON DTO 분리: Task 3
- Legacy JSON 타입 분리: Task 4
- 기능 불변 + 빌드 검증: Task 2~5
- `AOLTv1.0` 기능 복제 금지: 모든 Task에서 로직 수정 금지로 제한

### Placeholder scan

- `TODO`, `TBD`, “적절히”, “나중에” 같은 표현 없음
- 파일 경로와 검증 명령 명시됨

### Type consistency

- 모든 이동 대상 타입명이 설계 문서와 일치함
- subordinate face/plate 필드 유지 요구가 `BoundingBox` 단계에 반영됨
