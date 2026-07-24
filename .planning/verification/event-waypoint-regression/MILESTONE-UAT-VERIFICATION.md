---
status: pending
scope: phase-04, phase-05, phase-06, phase-08, gsd-quick
source:
  - 04-PPT-VERIFICATION.md
  - 04-PPT-SCENARIOS.md
---

# 마일스톤 및 Quick 작업 통합 UAT 검증

## 목적

이 문서는 기존 PPT 검증에서 수정 또는 재검증이 필요한 항목과 Phase 4, 5,
6, 8 작업, 그리고 Quick 작업을 한 번에 수동 검증하기 위한 기록지다.

각 항목의 상태는 `PASS`, `FAIL`, `BLOCKED`, `NOT RUN` 중 하나로 기록한다.
실패 시에는 영상명, 프레임 범위, 선택한 객체 또는 EventInstanceId, 재현 순서,
기대 결과, 실제 결과, 스크린샷 또는 JSON 증거를 함께 적는다.

참고: 계획 폴더의 `07-delete-face-and-plate-annotations-by-frame-or-waypoint`
는 기존 검증 보고서에서 Phase 8로 표기된 작업이다.

## 공통 준비

1. x64 Debug 프로그램을 실행한다.
2. EntryFrame과 ExitFrame이 다른 Event waypoint를 준비한다.
3. 서로 다른 EventInstanceId를 가지지만 기존 EventId가 같은 Event waypoint
   두 개를 준비한다.
4. 여러 프레임에 걸친 event box, face, plate, person body, vehicle body가 있는
   영상을 준비한다.
5. `event_exchange`, `event_board`, `event_disembark`, `event_camouflage`가
   포함된 기존 JSON 파일을 준비한다.

## 검증 항목

### UAT-01. Event waypoint 이름 동기화와 Undo/Redo (Phase 4)

- **관련 PPT:** PPT 3
- **재현:** 한 Event waypoint 안에서 사각형 위치가 서로 다른 event box를 만들고,
  한 프레임에서 이벤트 종류를 변경한다.
- **확인:** 같은 EventInstanceId의 모든 활성 event box, Event waypoint 행,
  현재 프레임 Event 패널을 확인한다. 동일한 기존 EventId를 가진 다른
  EventInstanceId도 확인한다. 이후 Undo 한 번, Redo 한 번을 수행한다.
- **기대:** 선택한 EventInstanceId의 모든 활성 box만 새 이벤트 종류로 변경된다.
  다른 EventInstanceId는 변경되지 않는다. 행과 패널은 즉시 갱신된다. Undo와
  Redo는 각각 한 번으로 해당 waypoint 전체 변경을 되돌리고 다시 적용한다.
- **상태:** `NOT RUN`
- **증거/비고:**

### UAT-02. Event waypoint 패널과 Exit 이동 (Phase 5 + Quick)

- **관련 PPT:** PPT 2, PPT 9
- **재현:** Event waypoint 패널의 Entry, Exit, Object 열을 확인한다. Entry와
  Exit이 다른 행에서 Entry, Exit, Object를 순서대로 클릭한다. Object 열만
  더블클릭해 interacting-object 편집기를 연다. Person waypoint를 선택한 뒤
  Event 또는 Vehicle waypoint를 선택해 삭제하고, 빈 공간 클릭 후 다시 삭제한다.
- **기대:** 열 순서는 Entry, Exit, Object이며 시간은 영상 시간으로 표시된다.
  Entry 클릭은 EntryFrame, Exit 클릭은 ExitFrame으로 이동한다. Object 클릭은
  기존처럼 EntryFrame의 대상 box를 선택한다. 인라인 편집기는 Object 열에서만
  열리고 Exit 열에서는 열리지 않는다. 삭제는 현재 활성 목록 항목만 대상으로
  하며 빈 공간 클릭 뒤에는 이전 Person waypoint가 삭제되지 않는다.
- **상태:** `NOT RUN`
- **증거/비고:**

### UAT-03. Event 생성, 수명, 정리 PPT 재검증

- **관련 PPT:** PPT 1, 3, 4, 6, 7, 8, 12, 13
- **재현:** Entry 후 Exit 또는 X로 event를 확정한다. Exit 프레임에 box가 있는
  경우와 없는 경우를 각각 수행한다. ExitFrame과 ExitFrame 다음 프레임을
  확인한다. 프레임 단위로 event box를 삭제하고, 객체가 겹친 경우도 수행한다.
- **기대:** 각 확정 동작마다 Event waypoint는 하나만 생성된다. event box는
  자신의 ExitFrame 다음 프레임에서 사라진다. 프레임 단위 삭제 후 고아 event
  waypoint 또는 ghost `contact` waypoint가 남지 않는다. 객체 겹침은 event
  수명을 연장하거나 삭제를 막지 않는다.
- **상태:** `NOT RUN`
- **증거/비고:**

### UAT-04. Event 사각형 전파와 이력 (Phase 6)

- **재현:** 하나의 EventInstanceId에서 source event box를 이동하거나 크기를
  변경한다. 이후의 자동 생성 box, 이후 수동 조정 box, 다른 EventInstanceId,
  삭제된 box를 비교한다. 한 프레임의 event box를 삭제한 뒤 다음 프레임도
  확인한다. 각 편집 후 Undo와 Redo를 한 번씩 수행한다.
- **기대:** 같은 EventInstanceId의 이후 자동 생성 box만 변경된다. 이전 box,
  이후 수동 조정 box, 다른 인스턴스, 삭제된 tombstone은 유지된다. 프레임 단위
  삭제는 이후 프레임 box를 삭제하지 않는다. Undo/Redo 한 번으로 편집 전체가
  되돌아가거나 재적용된다. 캔버스, Event 목록, waypoint 목록은 프레임 이동 없이
  즉시 갱신된다.
- **상태:** `NOT RUN`
- **증거/비고:**

### UAT-05. Face 및 Plate 삭제 (Phase 8)

- **재현:** 한 프레임의 face와 plate를 각각 선택해 `G`를 누른다. 이후 같은
  부모 waypoint 안의 face 또는 plate를 선택해 Delete를 누른다. 다른 객체의
  child, body, waypoint 행을 확인한다. 각 삭제 후 Undo를 수행하고 JSON을
  내보낸다.
- **기대:** `G`는 선택한 현재 프레임의 child만 삭제한다. Delete는 같은 부모
  waypoint 안의 같은 subtype child만 삭제한다. 다른 child, person/vehicle body,
  waypoint 행은 유지된다. Undo는 정확한 child를 복원한다. 내보낸 JSON은 삭제된
  child annotation/link를 제외하고 활성 body annotation은 유지한다.
- **상태:** `NOT RUN`
- **증거/비고:**

### UAT-06. 한글 UI 문자열 복구 (Quick)

- **재현:** person, vehicle, event box를 차례로 선택하고 Object Info, label 패널,
  attribute label을 확인한다.
- **기대:** 검사한 UI의 한글 문자열이 물음표 또는 깨진 문자 없이 정상적으로
  표시된다.
- **상태:** `NOT RUN`
- **증거/비고:**

### UAT-07. Event 카탈로그와 기존 JSON 호환 (Quick)

- **재현:** Event dropdown을 열고 목록 순서를 확인한다. 새 JSON을 저장해
  categories와 annotation의 category_id를 확인한다. `event_get on`과
  `event_suspect`를 각각 선택한 뒤 새 Event waypoint를 생성한다. 기존 JSON을
  불러온 뒤 event label을 확인하고 새 파일로 저장한 후 다시 불러온다.
- **기대 목록:** `event_contact`, `event_throw`, `event_final_exchange`,
  `event_get on`, `event_get off`, `event_suspect`,
  `event_controlled_delivery`, `event_camouflage` 순서다.
- **기대 새 ID:** contact 25, throw 26, final_exchange 27, get on 28,
  get off 29, suspect 30, controlled_delivery 31, camouflage 32다.
- **기대 생성 동작:** 생성 전 dropdown에서 고른 Event label이 새 event box와
  새 Event waypoint에 즉시 적용된다. 새 waypoint가 `event_contact`로 고정되어
  생성된 뒤 별도 수정이 필요한 경우가 없어야 한다.
- **기대 기존 JSON 변환:** `event_exchange -> event_throw`,
  `event_board -> event_get on`, `event_disembark -> event_get off`,
  `event_camouflage -> event_camouflage`, `event_throw -> event_throw`다.
  categories[].name이 있는 JSON은 이름을 숫자 ID보다 우선해 해석한다.
- **상태:** `NOT RUN`
- **증거/비고:**

## 결과 요약

| 항목 | 상태 | 비고 |
|---|---|---|
| UAT-01 | NOT RUN | Phase 4 |
| UAT-02 | NOT RUN | Phase 5 + Quick |
| UAT-03 | NOT RUN | PPT event 생성/수명/정리 |
| UAT-04 | NOT RUN | Phase 6 |
| UAT-05 | NOT RUN | Phase 8 |
| UAT-06 | NOT RUN | Quick 한글 UI |
| UAT-07 | NOT RUN | Quick event 카탈로그/JSON |

## 자동 검증 기준선

- 회귀 테스트: event 카탈로그 Quick 변경 이후 66건 통과.
- Debug x64 빌드: 별도 출력 경로에서 오류 0건.

## 최종 서명

| 역할 | 이름 | 날짜 | 결과 |
|---|---|---|---|
| QA | | | |
| 개발 | | | |
