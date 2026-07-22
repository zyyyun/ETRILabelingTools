# PPT 검증 리포트

**마일스톤:** `v1.0 Event Waypoint Stabilization`  
**작성일:** 2026-07-22  
**판정 상태:** `Fixed`, `Partial`, `Blocked`

## 요약

| Status | Count |
|--------|-------|
| Fixed | 0 |
| Partial | 0 |
| Blocked | 0 |
| Pending Manual/Hybrid | 14 |

## 수집된 자동 검증 증거

- 솔루션 빌드 기준:
  - `dotnet build ETRILabelingTool.sln`
- 경량 회귀 테스트 하네스:
  - `C:\Program Files\dotnet\dotnet.exe C:\Users\ANNA\Documents\ETRILabelingTools\WinFormsApp1.Tests\bin\Debug\net8.0-windows\WinFormsApp1.Tests.dll`
  - 2026-07-22 실행 결과: `45 tests passed`

## 검증 항목

### PPT 1
- **시나리오:** `G1` Event Finalization Uniqueness
- **재현 단계:** 기본 entry/exit 흐름으로 event waypoint를 생성하고, 두 번째 event가 생기는지 확인한다.
- **기대 결과:** event segment는 1개만 생성되고, 중복 event 사이에 exit timestamp와 entry timestamp가 엉키지 않는다.
- **실제 결과:** 수동 검증 대기. 자동 helper 테스트 기준으로는 event instance 재사용과 단일 finalization 동작이 반영되어 있다.
- **Status:** `Blocked`
- **비고:** 실제 앱 흐름에서 두 번째 event가 생기지 않는지 직접 UI 재현이 필요하다.

### PPT 2
- **시나리오:** `G5` Selection Ownership And Event List Editing
- **재현 단계:** event waypoint 리스트를 열고 interacting-object 필드를 인라인 편집한다.
- **기대 결과:** 편집기는 exit 칸이 아니라 객체 칸에 열려야 한다.
- **실제 결과:** 수동 검증 대기.
- **Status:** `Blocked`
- **비고:** 리스트 컬럼 동작은 현재 UI 레벨 자동 검증이 없다.

### PPT 3
- **시나리오:** `G2` Event Lifetime Hard Cap
- **재현 단계:** event를 확정한 뒤 `ExitFrame` 이후 프레임을 확인한다.
- **기대 결과:** event box는 `ExitFrame` 직후 바로 사라져야 한다.
- **실제 결과:** 자동 clamp 테스트는 통과했고, 직접 UI 재현은 아직 대기 상태다.
- **Status:** `Blocked`
- **비고:** 자동 증거는 helper 수준 trim 동작만 커버하고, 실제 런타임 재현은 아직 확인되지 않았다.

### PPT 4
- **시나리오:** `G1`, `G2`
- **재현 단계:** PPT에 적힌 대로 `Entry` 후 `E/X`를 사용하고, 보간/자동 추적 유무도 나눠서 확인한다.
- **기대 결과:** event waypoint는 1개만 생성되고, event box는 확정된 exit 이후까지 남지 않아야 한다.
- **실제 결과:** 수동 검증 대기.
- **Status:** `Blocked`
- **비고:** 자동 event finalization/clamp helper 결과로 일부는 뒷받침된다.

### PPT 5
- **시나리오:** `G3` Vehicle Independence And Duplicate Prevention
- **재현 단계:** vehicle이 겹친 상태에서 event를 만들고, 겹침 때문에 추가 확인 동작이나 vehicle side effect가 생기는지 본다.
- **기대 결과:** vehicle overlap만으로 추가 확인 동작이나 vehicle 상태 변경이 생기지 않아야 한다.
- **실제 결과:** 수동 검증 대기.
- **Status:** `Blocked`
- **비고:** 현재 자동 증거는 no-side-effect helper 로직만 커버하고, 팝업/UI 타이밍은 커버하지 않는다.

### PPT 6
- **시나리오:** `G1`
- **재현 단계:** exit 프레임에 bbox를 그리지 않고 `Exit` 버튼 또는 `X`로 event를 생성한다.
- **기대 결과:** event waypoint는 하나만 생성되어야 한다.
- **실제 결과:** 수동 검증 대기.
- **Status:** `Blocked`
- **비고:** 가장 기본적인 정상 경로이므로 실제 앱에서 직접 재현 확인이 필요하다.

### PPT 7
- **시나리오:** `G4` Event Cleanup And Ghost Waypoint Removal
- **재현 단계:** 프레임 단위 삭제 동작으로 event waypoint를 삭제하고 결과를 확인한다.
- **기대 결과:** 삭제되지 않던 event waypoint와 연결된 event box가 함께 제거되어야 한다.
- **실제 결과:** 하이브리드 검증 대기.
- **Status:** `Blocked`
- **비고:** helper 수준 cleanup은 반영됐지만, 정확한 프레임 단위 삭제 재현은 UI 확인이 필요하다.

### PPT 8
- **시나리오:** `G2`, `G4`
- **재현 단계:** 객체와 겹친 event가 객체 exit까지 남던 케이스를 재현하고, 이후 삭제도 시도한다.
- **기대 결과:** event 수명은 자기 exit에서 끝나고, event는 정상적으로 삭제 가능해야 한다.
- **실제 결과:** 하이브리드 검증 대기.
- **Status:** `Blocked`
- **비고:** 이 시나리오는 AOL 스타일 `manual-tracking-first` 규칙의 핵심 검증 포인트다.

### PPT 9
- **시나리오:** `G5`
- **재현 단계:** person waypoint를 선택한 뒤 vehicle/event waypoint 삭제를 시도한다.
- **기대 결과:** 현재 vehicle/event 리스트에서 수행한 동작이 우선되고, stale person selection 때문에 잘못 삭제되면 안 된다.
- **실제 결과:** selection-owner helper 자동 테스트는 통과했고, 직접 UI 재현은 대기 상태다.
- **Status:** `Blocked`
- **비고:** helper 로직은 자동 검증됐지만 end-to-end UI 확인이 남아 있어 하이브리드 항목이다.

### PPT 10
- **시나리오:** `G3`
- **재현 단계:** vehicle이 겹친 상태에서 exit-frame bbox를 그리고 auto-track으로 event waypoint를 생성한다.
- **기대 결과:** entry 프레임에 vehicle box가 두 개 생기지 않아야 한다.
- **실제 결과:** 수동 검증 대기.
- **Status:** `Blocked`
- **비고:** 런타임 overlap과 tracking 세팅이 필요하다.

### PPT 11
- **시나리오:** `G3`
- **재현 단계:** 하나의 `vehicle_car` 객체를 event-assisted 흐름에서 추적한다.
- **기대 결과:** 하나의 논리 차량은 하나의 identity만 유지하고 `01`, `02`로 쪼개지지 않아야 한다.
- **실제 결과:** vehicle identity 자동 테스트는 통과했고, 직접 UI 재현은 대기 상태다.
- **Status:** `Blocked`
- **비고:** helper 수준 identity 안정성은 확인됐지만, 실제 런타임 표시/동작은 추가 관찰이 필요하다.

### PPT 12
- **시나리오:** `G1`, `G3`
- **재현 단계:** exit-frame bbox를 그리고 auto-track으로 event waypoint를 생성한다.
- **기대 결과:** event waypoint 중복도, vehicle tracking box 중복도 생기지 않아야 한다.
- **실제 결과:** 수동 검증 대기.
- **Status:** `Blocked`
- **비고:** event와 vehicle side effect가 같이 엮인 UI-heavy 재현 항목이다.

### PPT 12-2
- **시나리오:** `G3`
- **재현 단계:** 같은 event-assisted auto-track 흐름을 motorcycle 또는 bicycle 클래스로 반복한다.
- **기대 결과:** 같은 논리 객체에 대해 중복 박스가 생성되지 않아야 한다.
- **실제 결과:** 수동 검증 대기.
- **Status:** `Blocked`
- **비고:** 특정 객체 클래스와 실제 런타임 재현이 필요하다.

### PPT 13
- **시나리오:** `G4`
- **재현 단계:** 설명된 유형의 event를 만들고 ghost `contact` waypoint 생성 및 지속 여부를 확인한다.
- **기대 결과:** 작업하지 않은 ghost `contact` waypoint가 생기거나 unrelated person이 사라질 때까지 유지되면 안 된다.
- **실제 결과:** 수동 검증 대기.
- **Status:** `Blocked`
- **비고:** 이 항목이 `QA-02`의 핵심 ghost/orphan 검증이다.

## 비고

- 현재 문서는 구조와 자동 증거를 먼저 채워 둔 상태라, 남은 작업은 리포트 작성이 아니라 수동 판정에 집중하면 된다.
- 수동 판정이 끝나면 요약 표의 status 개수를 업데이트해야 한다.
