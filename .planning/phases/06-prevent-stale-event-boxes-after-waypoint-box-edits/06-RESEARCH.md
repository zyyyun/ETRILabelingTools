# Phase 6: waypoint 박스 편집 후 stale event box 방지 - Research

**조사일:** 2026-07-23  
**도메인:** Windows Forms 이벤트 waypoint 박스 전파·수동 보정 우선순위  
**신뢰도:** HIGH

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions
- **D-01:** A manual move or resize of an event box propagates its rectangle
  from the current frame through the containing Event Waypoint's exit frame.
- **D-02:** Frames before the edited frame remain unchanged.
- **D-03:** A later frame that has been manually corrected is protected from
  automatic propagation. A new edit updates only the still automatically
  propagated portion of the waypoint.
- **D-04:** Deleting an event box on one frame removes only that frame's box;
  later event boxes remain intact.
- **D-05:** Deleting the Event Waypoint remains the only action that removes
  event boxes across the full waypoint range.
- **D-06:** After an edit or deletion, refresh both the current frame display
  and the Event Waypoint list immediately so the visible state reflects the
  change without navigation.

### the agent's Discretion
- Choose the smallest durable representation for distinguishing manual
  corrections from derived boxes, while preserving existing JSON compatibility.
- Reuse the established event identity and active-waypoint scope helpers;
  determine the focused regression coverage and refresh ordering.

### Deferred Ideas (OUT OF SCOPE)
- Synchronizing event boxes after the tracked person or vehicle changes is
  Phase 7 and is explicitly out of scope here.
- Broader changes to event taxonomy, JSON schema, or non-event waypoint
  behavior remain out of scope.
</user_constraints>

## Summary

현재 드래그와 리사이즈 완료 경로는 모두 `PropagateEventBoxFromCurrentFrame(selectedBox)`를 호출하지만, 메서드가 `event` 박스를 받으면 즉시 반환한다. 따라서 Phase 6의 주 변경점은 이 가드를 Phase 7의 자동 추적 가드와 혼동하지 않도록 제거하고, 이벤트 인스턴스/waypoint 범위 안에서만 순수하게 사각형을 동기화하는 것이다. [VERIFIED: codebase grep — `WinFormsApp1/Forms/Form1.Drawing.cs:414-457`, `WinFormsApp1/Forms/Form1.EventPropagation.cs:428-435`]

가장 작은 호환 가능한 모델은 새 JSON 필드를 만들지 않고, 이미 메모리에 존재하는 `manuallyAdjustedFrames: Dictionary<string,List<int>>`를 이벤트에도 사용해 `EventInstanceId` 기반 프레임 보호 집합으로 삼는 것이다. 키 생성은 이미 이벤트에 `event_instance_{EventInstanceId}`를 우선하는 `TrackingIdentityHelper.GetIdentityKey`를 경유한다. 이 상태는 저장 JSON으로 내보내지지 않으므로 기존 JSON 입출력 형식을 바꾸지 않는다. 단, 앱을 재시작한 뒤에는 과거 수동 보정의 출처를 복원할 수 없다는 범위 한계가 있다. [VERIFIED: codebase grep — `WinFormsApp1/Form1.cs:1341-1349`, `WinFormsApp1/Forms/Form1.Drawing.cs:2848-2868`, `WinFormsApp1/Forms/Form1.Helpers.cs:222-225`, `WinFormsApp1/Forms/Form1.Json.cs:1164-1165`]

**Primary recommendation:** `EventWaypointUpdateHelper.ResolveActiveScope`로 단일 event waypoint를 fail-closed로 해석한 뒤, 프레임별 존재/삭제/수동보정 상태를 판정하는 작은 순수 helper를 추가하고, UI 이벤트에서는 그 helper의 변경 결과만 적용·갱신한다. [VERIFIED: codebase grep — `WinFormsApp1/Logic/EventWaypointUpdateHelper.cs:62-119`]

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|---|---|---|---|
| 편집 범위 및 event 인스턴스 해석 | 애플리케이션 도메인 로직 | Windows Forms UI | `EventInstanceId`와 waypoint 범위가 데이터의 소유 경계를 정한다. [VERIFIED: codebase grep — `EventWaypointUpdateHelper.cs:72-119`] |
| 수동 이동·리사이즈 입력 | Windows Forms UI | 도메인 로직 | 마우스 이벤트가 편집 완료 시점을 확정하고 도메인 helper를 호출한다. [VERIFIED: codebase grep — `Form1.Drawing.cs:414-457`] |
| 프레임별 사각형 변경·단일 프레임 삭제 | 애플리케이션 도메인 로직 | 메모리 저장소 | `boundingBoxes`와 `IsDeleted`가 현재 주 데이터 상태다. [VERIFIED: codebase grep — `BoundingBox.cs:8-25`, `Form1.Drawing.cs:2752-2769`] |
| 즉시 화면·waypoint 목록 반영 | Windows Forms UI | 도메인 로직 | 변경 후 캐시 무효화 및 두 UI 표면을 갱신해야 한다. [VERIFIED: codebase grep — `Form1.EventPropagation.cs:503-508`, `Form1.Timeline.cs:719-725`] |

## Standard Stack

### Core

| Library / 구성 | Version | Purpose | Why Standard |
|---|---:|---|---|
| .NET Windows Forms + 기존 `Form1` partial | .NET 8.0 | 편집 이벤트와 UI 갱신 | 이 phase는 기존 데스크톱 앱의 in-process 상태 전이만 수정하며 새 패키지가 필요 없다. [VERIFIED: codebase grep — `WinFormsApp1.csproj`] |
| `EventWaypointUpdateHelper` | 프로젝트 내부 | event instance 범위 해석 | 이미 활성 event scope를 instance-first, legacy fallback, 모호 시 `null`로 해석한다. [VERIFIED: codebase grep — `EventWaypointUpdateHelper.cs:62-119`] |

**Installation:** 없음 — 외부 패키지를 설치하지 않는다. [VERIFIED: codebase grep — `WinFormsApp1.csproj`]

## Architecture Patterns

### System Architecture Diagram

```text
마우스 drag/resize 완료
        |
        v
RecordManuallyAdjustedFrame(현재 event box)
        |
        v
ResolveActiveScope(EventInstanceId, waypoint 범위) -- 모호함 --> 변경 없음 + 진단 로그
        |
        v
각 frame: 현재 frame+1 .. Exit
  ├─ 삭제된 동일-instance box 존재 ----> 유지 (절대 재생성하지 않음)
  ├─ 수동 보정 frame -----------------> 유지 (rectangle 덮어쓰지 않음)
  ├─ 활성 derived box -----------------> source rectangle로 갱신
  └─ box 자체가 없음 ------------------> derived 복제 생성
        |
        v
InvalidateBoxCache → UpdateBoxCount/UpdateBboxListDisplay
→ UpdateEventListDisplay + UpdateWaypointListView → pictureBoxVideo.Invalidate
```

### Recommended Project Structure

```text
WinFormsApp1/
├── Logic/
│   └── EventWaypointBoxPropagationHelper.cs # 순수 범위/보호/변경계획 계산
├── Forms/
│   ├── Form1.EventPropagation.cs            # helper 결과를 boundingBoxes에 적용
│   └── Form1.Drawing.cs                     # drag/resize 완료 후 단일 호출
└── Tests/
    └── Program.cs                            # helper 회귀 시나리오
```

### Pattern 1: Instance-first, fail-closed 범위 해석

**What:** 편집 대상의 `EventInstanceId`가 일치하는 waypoint 하나만 대상으로 하고, legacy 데이터는 helper의 기존 범위 제한 fallback만 사용한다. [VERIFIED: codebase grep — `EventWaypointUpdateHelper.cs:79-118`]

**When to use:** event 사각형의 위치 변경, 삭제 판정, type 변경처럼 event segment 전체가 아닌 “현재 event segment”만 건드려야 하는 모든 경로. [VERIFIED: codebase grep — `EventWaypointUpdateHelper.cs:54-60`]

**Why:** 현재 전파 구현은 `EventId`만 비교하므로 같은 event type을 가진 다른 segment의 box까지 바꿀 수 있고, waypoint 탐색도 label/프레임만 비교한다. [VERIFIED: codebase grep — `Form1.EventPropagation.cs:441-465`]

### Pattern 2: 프레임별 3상태 보호

**What:** 같은 instance의 후속 프레임을 “삭제됨”, “수동 보정됨”, “derived/없음”으로 분기한다. 삭제됨과 수동 보정됨은 source rectangle로 덮어쓰지 않는다. [VERIFIED: codebase grep — `BoundingBox.cs:18-25`, `Form1.Drawing.cs:2848-2868`]

**When to use:** D-01~D-04의 edit propagation 경로에만 사용한다. 자동 추적·보간이나 tracked-object 변경은 Phase 7 범위이므로 이 helper를 호출하지 않는다. [CITED: `.planning/phases/06-prevent-stale-event-boxes-after-waypoint-box-edits/06-CONTEXT.md`]

**Example (구현 설계용 의사 코드):**

```csharp
// Source: existing scope and state conventions
// [VERIFIED: codebase grep — EventWaypointUpdateHelper.cs, BoundingBox.cs]
foreach (var frame in Enumerable.Range(source.FrameIndex + 1,
             scope.Waypoint.ExitFrame - source.FrameIndex))
{
    var existing = FindSameInstanceBoxAtFrame(frame); // deleted 포함
    if (existing?.IsDeleted == true) continue;        // 단일 프레임 삭제 보호
    if (manualFrames.Contains(frame)) continue;       // 후속 수동 보정 보호

    if (existing != null) existing.Rectangle = source.Rectangle;
    else AddDerivedEventBox(source, frame);           // source의 instance ID 복제
}
```

### Anti-Patterns to Avoid

- **`EventId`만으로 범위 선택:** 같은 이벤트 종류의 다른 waypoint를 오염시킬 수 있다. `ResolveActiveScope`의 `EventInstanceId` 우선 규칙을 재사용한다. [VERIFIED: codebase grep — `Form1.EventPropagation.cs:461-465`, `EventWaypointUpdateHelper.cs:79-103`]
- **`updatedCount == 0`일 때만 전체 구간 생성:** 하나의 삭제 box 또는 보호된 수동 box가 있으면 빈 프레임을 처리하지 못하거나, 반대로 삭제 프레임을 되살리는 조건으로 변질되기 쉽다. 생성 여부는 각 프레임의 동일-instance box 존재 여부로 판단한다. [VERIFIED: codebase grep — `Form1.EventPropagation.cs:467-500`]
- **모든 Delete/G 키 경로에서 `RecordDisappearanceIntent` 유지:** 현재 일반 box 삭제는 range disappearance로 기록한다. event의 D-04에는 단일 프레임 tombstone만 남겨야 하므로 event box 삭제는 이 기록을 건너뛰어야 한다. [VERIFIED: codebase grep — `Form1.Shortcuts.cs:488-500`, `Form1.Drawing.cs:2872-2895`]
- **UI 갱신을 propagation 내부 일부 경로에만 두기:** 현재 전파는 bbox 목록만 갱신하고 event waypoint 목록을 갱신하지 않는다. 성공·무변경·삭제 공통의 UI 갱신 경로를 호출한다. [VERIFIED: codebase grep — `Form1.EventPropagation.cs:503-508`, `Form1.Drawing.cs:2752-2769`]

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---|---|---|---|
| event segment 식별 | `EventId + Rectangle` 비교 | `EventWaypointUpdateHelper.ResolveActiveScope` + `EventInstanceId` | 위치 변경은 identity가 아니며, 기존 helper는 모호한 legacy scope를 거부한다. [VERIFIED: codebase grep — `EventWaypointUpdateHelper.cs:62-119`] |
| event waypoint 전체 삭제 | 별도의 frame loop | `EventFinalizationHelper.GetEventBoxesForWaypoint` | 기존 waypoint 삭제 경로가 instance-aware cleanup을 사용한다. [VERIFIED: codebase grep — `Form1.Timeline.cs:692-704`] |
| 수동 프레임 추적 저장소 | 새 JSON schema 또는 새 전역 저장소 | 기존 `manuallyAdjustedFrames`와 `GetDrawingIdentityKey` | 추가 패키지·JSON schema 변화 없이 event instance에 충돌 없이 연결된다. [VERIFIED: codebase grep — `Form1.cs:1341-1342`, `Form1.Helpers.cs:222-225`] |

**Key insight:** 전파의 핵심은 “source rect를 복제”가 아니라 “각 target frame의 의도를 보존한 상태 전이”이다. 삭제 marker와 수동 marker는 서로 다른 의미이므로 하나의 `exists` 검사로 합치면 안 된다. [VERIFIED: codebase grep — `BoundingBox.cs:25`, `Form1.Drawing.cs:2848-2895`]

## Common Pitfalls

### Pitfall 1: 현재 event 전파 가드만 제거하는 수정

**What goes wrong:** `EventId` 기준 loop가 같은 type의 다른 event segment까지 덮어쓴다. [VERIFIED: codebase grep — `Form1.EventPropagation.cs:461-465`]

**How to avoid:** 먼저 `ResolveActiveScope`가 반환한 waypoint와 instance matcher로 후보를 제한하고, `null`이면 아무 것도 변경하지 않는다. [VERIFIED: codebase grep — `EventWaypointUpdateHelper.cs:88-103`]

### Pitfall 2: 삭제된 frame의 box 재생성

**What goes wrong:** `IsDeleted` box를 “없음”으로 간주하면 이후 편집이 단일-frame 삭제를 되돌린다. [VERIFIED: codebase grep — `BoundingBox.cs:25`, `Form1.Drawing.cs:2760-2769`]

**How to avoid:** frame lookup은 deleted box도 찾고, `IsDeleted`면 skip한다. event box의 G/Delete 처리에서는 `RecordDisappearanceIntent`를 호출하지 않는다. [VERIFIED: codebase grep — `Form1.Shortcuts.cs:488-546`]

### Pitfall 3: undo와 cache/UI 일관성 누락

**What goes wrong:** 현재 edit undo는 source box 하나만 snapshot하며, 전파로 바뀐 후속 boxes는 별도 snapshot이 없으면 undo/redo가 부분 상태가 된다. [VERIFIED: codebase grep — `Form1.Drawing.cs:419-431`, `Form1.Undo.cs`]

**How to avoid:** helper가 만든 update/create 목록에 대해 전파 전 snapshot을 기록하거나, 전파 작업을 하나의 복합 undo action으로 표현할 수 있는지 먼저 확인한다. 현 undo 모델의 multi-box 지원 여부는 구현 전 점검 항목이다. [ASSUMED]

### Pitfall 4: 목록은 오래된 상태인데 캔버스만 갱신

**What goes wrong:** D-06과 달리 event list row가 사용자가 방금 편집/삭제한 상태를 즉시 반영하지 않는다. [CITED: `.planning/phases/06-prevent-stale-event-boxes-after-waypoint-box-edits/06-CONTEXT.md`]

**How to avoid:** mutation 뒤 순서를 `InvalidateBoxCache → counts/bbox list → Event list + waypoint list → timeline/canvas invalidate`로 통일한다. [VERIFIED: codebase grep — `Form1.Timeline.cs:719-725`]

## Code Examples

### Edit completion integration

```csharp
// Source: existing drag/resize completion convention
// [VERIFIED: codebase grep — Form1.Drawing.cs:414-457]
RecordManuallyAdjustedFrame(selectedBox);
if (selectedBox?.Label == "event")
{
    SynchronizeEventRectangleFromEdit(selectedBox); // Phase 6 helper seam
}
RefreshEventEditSurfaces(); // event list, waypoint list, canvas
```

### Single-frame event deletion integration

```csharp
// Source: existing delete uses IsDeleted tombstones
// [VERIFIED: codebase grep — Form1.Drawing.cs:2752-2769]
selectedBox.IsDeleted = true;
// Event box: do NOT call RecordDisappearanceIntent.
// Do not remove later same-instance boxes and do not remove the waypoint.
RefreshEventEditSurfaces();
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|---|---|---|---|
| Rectangle equality / `EventId` selection | `EventInstanceId`-first event scope helper | Existing Phase 4-era code | Geometry changes do not split identity; Phase 6 should use this seam rather than revive rectangle matching. [VERIFIED: codebase grep — `EventWaypointUpdateHelper.cs:79-118`, `Form1.Drawing.cs:2777-2823`] |
| Event edit propagation intentionally disabled | Phase 6 enables scoped rectangle synchronization | Planned | Automatic tracking remains disabled and belongs to Phase 7. [VERIFIED: codebase grep — `Form1.EventPropagation.cs:428-435`; [CITED: `06-CONTEXT.md`]] |

## Assumptions Log

| # | Claim | Section | Resolution |
|---|---|---|---|
| A1 | Event edits require a dedicated grouped history action because `ModifyBox` is a single-box entry and is pushed before propagation in the resize path. | Common Pitfalls | Resolved: replace or merge that entry so the source snapshot, propagated updates, and additions are one transaction. [VERIFIED: `Form1.Drawing.cs:419-431`, `Form1.Undo.cs:47-60`; DECISION: Phase 6 revision] |
| A2 | Manual-frame provenance remains in memory only in Phase 6. | Summary | Resolved: preserve JSON compatibility; cross-session provenance is deferred. [VERIFIED: `Form1.cs:1341-1342`, `Form1.Json.cs:1164-1165`; DECISION: Phase 6 revision] |

## Open Questions (RESOLVED)

1. **Manual-correction provenance persistence:** Phase 6 uses the existing in-memory `manuallyAdjustedFrames` collection keyed by `event_instance_{EventInstanceId}`. It does not add JSON fields or alter import/export; therefore, provenance does not survive application restart. Cross-session provenance is explicitly deferred, while same-session D-03 protection is required. [VERIFIED: `Form1.cs:1341-1342`, `Form1.Json.cs:1164-1165`; DECISION: Phase 6 revision]

2. **Propagation-batch Undo scope:** One manual event-box drag or resize is one Undo/Redo transaction. The transaction includes the source box's before/after rectangle, every helper-planned existing-box rectangle mutation, and every helper-planned created box. The event-edit path must replace or merge the pre-existing `ModifyBox` entry in `Form1.Drawing.cs`; it must never push that entry and then add a second propagation action. Undo restores the source and all existing targets and removes created targets; Redo reapplies all three categories. [VERIFIED: `Form1.Drawing.cs:419-431`, `Form1.Undo.cs:47-60`; DECISION: Phase 6 revision]

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|---|---|---:|---|---|
| .NET SDK | build 및 regression harness | ✓ | 9.0.304 | 프로젝트 target은 .NET 8.0이며 현재 build/run 가능. [VERIFIED: local command `dotnet --version`, `WinFormsApp1.csproj`] |
| Windows Forms regression harness | helper 회귀 테스트 | ✓ | `WinFormsApp1.Tests` | `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore`가 56개 PASS. [VERIFIED: local command] |

**Missing dependencies with no fallback:** 없음. [VERIFIED: local command]

## Validation Architecture

### Test Framework

| Property | Value |
|---|---|
| Framework | 자체 console regression harness (`WinFormsApp1.Tests`, .NET 8 Windows target). [VERIFIED: codebase grep — `WinFormsApp1.Tests.csproj`, `Program.cs:5-75`] |
| Config file | 없음; `Program.cs`의 `(Name, Action)` 목록이 테스트 등록부다. [VERIFIED: codebase grep — `Program.cs:7-75`] |
| Quick run command | `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` [VERIFIED: local command] |
| Full suite command | `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` [VERIFIED: local command] |

### Phase Requirements → Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|---|---|---|---|---|
| TBD | current frame edit부터 exit까지 derived boxes가 갱신되고 이전 frame은 유지됨 | unit | `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` | ❌ Wave 0 |
| TBD | 후속 수동 보정 frame은 전파에서 보호됨 | unit | 동일 | ❌ Wave 0 |
| TBD | deleted event frame은 재생성되지 않고 후속 box는 유지됨 | unit | 동일 | ❌ Wave 0 |
| TBD | 다른 `EventInstanceId` 및 모호한 scope는 변경하지 않음 | unit | 동일 | ❌ Wave 0 |
| TBD | drag/resize 및 Delete 뒤 event/waypoint UI가 즉시 갱신됨 | manual UI smoke | 앱 실행 후 확인 | ❌ manual |

### Sampling Rate

- **Per task commit:** `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` [VERIFIED: local command]
- **Per wave merge:** `dotnet build ETRILabelingTool.sln -c Debug -p:Platform=x64` [CITED: `AGENTS.md`]
- **Phase gate:** regression harness green + 편집/삭제 UI manual smoke. [ASSUMED]

### Wave 0 Gaps

- [ ] `WinFormsApp1.Tests/Program.cs` — pure propagation helper의 range, manual-precedence, deletion-tombstone, instance-isolation 회귀 테스트 등록. [VERIFIED: codebase grep — `Program.cs:9-67`]
- [ ] helper가 UI 없이 테스트 가능하도록 `Logic/EventWaypointBoxPropagationHelper.cs` 또는 `EventWaypointUpdateHelper`의 순수 API를 추가. [ASSUMED]

## Security Domain

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---|---|---|
| V2 Authentication | no | 로컬 Windows Forms annotation state 변경이며 인증 흐름은 범위 밖이다. [VERIFIED: codebase grep — phase context and project structure] |
| V3 Session Management | no | 동일. [VERIFIED: codebase grep — phase context and project structure] |
| V4 Access Control | no | 동일. [VERIFIED: codebase grep — phase context and project structure] |
| V5 Input Validation | yes | event 여부, non-null scope, frame range, instance identity를 helper에서 검증하고 모호하면 fail closed. [VERIFIED: codebase grep — `EventWaypointUpdateHelper.cs:67-119`] |
| V6 Cryptography | no | 암호 처리 없음. [VERIFIED: codebase grep — phase context and project structure] |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---|---|---|
| 잘못된 instance의 rectangle 덮어쓰기 | Tampering | `EventInstanceId`-first scope 및 모호한 legacy scope 거부. [VERIFIED: codebase grep — `EventWaypointUpdateHelper.cs:79-119`] |
| 단일-frame 삭제의 자동 복구 | Tampering | deleted tombstone을 존재 상태로 취급해 propagation skip. [VERIFIED: codebase grep — `BoundingBox.cs:25`] |

## Project Constraints (from AGENTS.md)

- .NET 8.0 Windows Forms application이며 기본 x64 Debug build는 `dotnet build ETRILabelingTool.sln -c Debug -p:Platform=x64`이다. [CITED: `AGENTS.md`]
- 핵심 UI/로직은 대형 `Form1` partial에 있고, 이번 phase는 해당 구조와 OpenCV/YOLO 경로를 불필요하게 넓히지 않는다. [CITED: `AGENTS.md`]
- UI/comments는 주로 한국어이고 JSON attribute values는 영어를 유지한다. [CITED: `AGENTS.md`]

## Sources

### Primary (HIGH confidence)

- [codebase] `WinFormsApp1/Forms/Form1.Drawing.cs` — drag/resize 완료, 단일 box 삭제, manual/disappearance 기록.
- [codebase] `WinFormsApp1/Forms/Form1.EventPropagation.cs` — 비활성화된 event edit propagation과 기존 frame loop.
- [codebase] `WinFormsApp1/Logic/EventWaypointUpdateHelper.cs` — instance-first active scope 및 fail-closed legacy 처리.
- [codebase] `WinFormsApp1.Tests/Program.cs` — 현재 경량 regression harness 관례.
- [local command] `dotnet run --project WinFormsApp1.Tests/WinFormsApp1.Tests.csproj --no-restore` — 56 tests PASS.

### Secondary (MEDIUM confidence)

- [project decision] `.planning/phases/06-prevent-stale-event-boxes-after-waypoint-box-edits/06-CONTEXT.md` — D-01~D-06 및 scope fence.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — 새 package 없이 기존 .NET/WinForms 및 내부 helper만 사용한다. [VERIFIED: codebase grep]
- Architecture: HIGH — 실제 mouse handler, propagation seam, scope helper를 직접 추적했다. [VERIFIED: codebase grep]
- Pitfalls: HIGH — 현재 disabled guard, EventId-only filter, deletion/disappearance 경로에서 직접 확인했다. [VERIFIED: codebase grep]

**Research date:** 2026-07-23  
**Valid until:** 2026-08-22 — codebase 내부 설계 조사이며 phase 구현 전까지. [ASSUMED]
