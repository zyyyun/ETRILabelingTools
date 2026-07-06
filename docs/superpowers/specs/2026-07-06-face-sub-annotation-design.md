# Face Sub-Annotation Design

## Goal

기존 `person` 라벨링 흐름을 유지하면서 얼굴을 `person_id`의 하위 annotation으로 추가한다. 사용자는 `Draw` 모드에서 `Person` 선택 후 좌클릭 드래그로 body BBOX를, 우클릭 드래그로 face BBOX를 생성할 수 있어야 한다. face는 독립 waypoint를 만들지 않고 기존 body waypoint 내부 구간에서만 존재하며, JSON은 기존 구조와 호환되어야 한다.

## Scope

이번 설계는 `face_id`에 해당하는 person 하위 annotation 흐름만 다룬다.

- 포함
  - Draw 모드 좌/우클릭 분기
  - face 생성 가능 조건
  - face의 box-level entry/exit
  - 기존 person 관성 보간 로직 재사용
  - JSON 저장/로드 호환 규칙
  - 라벨 표기 및 가시성 개선
- 제외
  - vehicle plate 구현
  - event 종류 확장
  - Person Attributes의 Camouflage 추가 구현

## Recommended Approach

추천안은 기존 `person` 워크플로우를 확장하는 방식이다.

- `Label = "person"`은 유지한다.
- face는 별도 top-level collection이 아니라 기존 `annotations[]` 안의 `person` annotation으로 저장한다.
- 구분은 `attributes.person_part_type = "face"`와 `linked_person_id`로 한다.
- face는 별도 waypoint를 만들지 않고 body waypoint 내부에서만 `box_entry_frame`과 `box_exit_frame`를 가진다.
- 자동화는 body와 face를 분리한다.
  - body: 기존대로 waypoint Entry/Exit 확정 후 YOLO 추적
  - face: box-level Entry/Exit 확정 후 기존 person 관성 보간 로직 자동 실행

이 접근은 기존 JSON consumer와의 호환성이 가장 좋고, 현재 코드베이스의 person/waypoint/보간 구조를 크게 흔들지 않는다.

## Runtime Behavior

### Draw mode input

- `Person + 좌클릭 드래그`는 기존 body BBOX 생성이다.
- `Person + 우클릭 드래그`는 face BBOX 생성 시도이다.
- Select 모드 우클릭은 기존 person attributes 팝업을 그대로 유지한다.

### Face creation guard

face는 현재 프레임에 활성 person waypoint가 있을 때만 생성할 수 있다.

- 현재 프레임에 활성 person waypoint가 없으면 face 생성은 차단한다.
- 사용자에게 짧은 안내 메시지를 보여준다.
  - 예: `현재 프레임에 활성 Person waypoint가 없어 face를 생성할 수 없습니다. 먼저 body를 라벨링하세요.`

### Face-to-body linking

face는 사용자가 직접 ID를 고르지 않는다. 생성 시점에 현재 프레임의 body 후보와 자동 연결한다.

- 후보는 현재 프레임에서 활성인 body person BBOX들이다.
- 우클릭 드래그로 만든 face rectangle과 가장 많이 겹치는 body BBOX를 우선 선택한다.
- 겹침 면적이 같으면 중심점 거리가 더 가까운 body를 선택한다.
- 선택된 body의 `person_id`를 `linked_person_id`에 저장한다.

### Face lifetime

face는 body처럼 waypoint를 만들지 않는다. 대신 body waypoint 내부에서만 별도 box-level range를 가진다.

- face 생성 시 `box_entry_frame = 생성 프레임`
- face 종료 시 `box_exit_frame = 종료 프레임`
- `box_entry_frame`과 `box_exit_frame`는 body waypoint 범위 밖으로 나갈 수 없다.

### Face exit UX

기존 Exit 동작을 재사용한다.

- 선택된 박스가 body 또는 event이면 기존 Exit 동작을 유지한다.
- 선택된 박스가 face이면 waypoint 종료가 아니라 `box_exit_frame`만 갱신한다.
- `box_exit_frame < box_entry_frame`이면 저장하지 않고 오류를 표시한다.

## Data Model

`BoundingBox`에 다음 필드를 추가한다.

- `PersonPartType`
  - `null` 또는 `"body"`: 기존 body
  - `"face"`: 얼굴 하위 annotation
- `LinkedPersonId`
  - face가 연결된 body의 `person_id`
- `BoxEntryFrame`
  - face 표시 시작 프레임
- `BoxExitFrame`
  - face 표시 종료 프레임

body는 기존 waypoint 중심 모델을 유지한다. face는 waypoint를 만들지 않고 위 필드들만 사용한다.

## JSON Format

### Storage rule

기존 `annotations[]` 구조를 유지한다. face도 같은 배열에 들어간다.

예시:

```json
{
  "annotations": [
    {
      "id": 1,
      "image_id": 101,
      "category_id": 3,
      "bbox": [820, 220, 180, 420],
      "track_id": 3,
      "attributes": {
        "person_part_type": "body"
      }
    },
    {
      "id": 2,
      "image_id": 101,
      "category_id": 3,
      "bbox": [860, 250, 64, 64],
      "track_id": 3,
      "attributes": {
        "person_part_type": "face",
        "linked_person_id": 3,
        "box_entry_frame": 1500,
        "box_exit_frame": 1620
      }
    }
  ]
}
```

### Compatibility rule

- 기존 reader는 face annotation도 일반 `person` annotation으로 읽을 수 있다.
- 새 로직만 `attributes.person_part_type == "face"`를 보고 face로 해석한다.
- body는 구버전 호환을 최우선으로 할 경우 `person_part_type`이 없어도 된다.
- 새 로직에서는
  - `person_part_type == "face"`이면 face
  - 값이 없거나 `"body"`이면 body
  로 처리한다.

### Load rule

JSON 로드 시 다음을 복원한다.

- `PersonPartType`
- `LinkedPersonId`
- `BoxEntryFrame`
- `BoxExitFrame`

값이 없으면 기존 body annotation으로 간주한다.

## Tracking and Interpolation

### Body automation

기존 body person BBOX는 변경하지 않는다.

- waypoint Entry/Exit 확정 후 기존대로 YOLO 자동 추적

### Face automation

face는 YOLO 대상이 아니다.

- face의 `box_entry_frame`과 `box_exit_frame`가 확정되면
  기존 person의 관성 보간 로직을 그대로 사용한다.
- 새 보간 엔진을 만들지 않는다.
- 내부적으로는 기존 `R` 키로 잡는 `a 프레임`, `Shift+T`로 수행하는 `a~b` 보간 흐름과 같은 경로를 사용한다.

### Manual correction support

중간 보간 동작은 기존 person과 동일해야 한다.

- 사용자가 중간 프레임에서 face 박스를 수동 수정하면 그 프레임을 기준점으로 기록한다.
- 이후 보간은 기존 person 보간처럼 수동 수정 프레임을 기준으로 다시 계산한다.
- 즉 `person_id`의 기존 보간/관성 추적 규칙을 face에도 동일하게 적용한다.

## UI and Visuals

### Label text

사용자가 body와 face를 구분할 수 있어야 한다.

- body 예: `person_body_03`
- face 예: `person_face->body_03`

### Box visibility

body/face가 겹치는 경우가 많으므로 테두리 두께를 현재보다 얇게 조정한다. 선택 상태의 강조도 같이 얇아진 기본 두께를 기준으로 맞춘다.

## Error Handling

- body waypoint 없음: face 생성 차단 + 안내 메시지
- body 후보 없음: face 생성 취소 + 안내 메시지
- `box_exit_frame < box_entry_frame`: 종료 저장 차단 + 오류 메시지
- body와의 연결이 실패한 face는 저장하지 않는다

## Verification Criteria

- Draw 모드에서 `Person + 좌드래그`는 기존처럼 body BBOX를 만든다.
- Draw 모드에서 `Person + 우드래그`는 face BBOX 생성 시도로 들어간다.
- 현재 프레임에 활성 person waypoint가 없으면 face 생성이 차단된다.
- face는 현재 프레임에서 가장 많이 겹치는 body의 `person_id`에 연결된다.
- face는 JSON의 `annotations[]` 안에 저장된다.
- face annotation에는 `person_part_type = "face"`와 `linked_person_id`가 들어간다.
- `box_entry_frame`, `box_exit_frame`가 저장되고 다시 로드된다.
- 기존 JSON 파일 로드가 깨지지 않는다.
- face 선택 상태에서 Exit를 누르면 body waypoint가 아니라 face의 `box_exit_frame`만 닫힌다.
- face entry/exit 확정 후 기존 person 관성 보간 로직이 자동 실행된다.
- `R` / `Shift+T` 기반 수동 보간도 face에서 동일하게 동작한다.
- face는 YOLO 자동 추적 대상에서 제외된다.

## Risks and Boundaries

- 자동 연결을 겹침 기반으로 하면 밀집 장면에서 오연결 가능성이 있다.
- 이번 단계에서는 그 위험을 감수하고 선택 팝업은 두지 않는다.
- 이후 vehicle plate를 넣을 때는 동일 패턴을 재사용할 수 있지만, 이번 설계는 face만 대상으로 한다.
