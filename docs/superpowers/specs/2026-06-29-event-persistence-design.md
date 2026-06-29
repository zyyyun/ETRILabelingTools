# Event 저장 안정성 설계서

## 목표
저장 후 다시 로드했을 때 Event 라벨링 데이터가 생겼다 사라지는 문제를 해결한다.
동시에 현재 툴과 다른 후속 툴들이 사용하는 기존 JSON 구조와의 호환성을 최대한 유지한다.

## 범위
이 설계서는 Event 저장 안정성 문제만 다룬다.
다음 항목은 이번 범위에 포함하지 않는다.
- Entry/Exit UI 분할 개편
- 보간(Interpolation) 백엔드 개선
- 자동 라벨링 Freeze 개선

## 문제 요약
현재 툴에서는 같은 Event 종류가 한 영상 안에 여러 번 등장할 수 있다.
예를 들어 `contact` Event가 서로 다른 구간에 여러 개 존재하는 것이 가능하다.

하지만 현재 코드의 여러 경로는 Event를 `EventId` 중심으로 식별하고 있다.
문제는 `EventId`가 개별 Event 인스턴스의 고유 식별자가 아니라, Event의 종류를 나타내는 값이라는 점이다.
이 때문에 서로 다른 Event 인스턴스가 저장 또는 로드 과정에서 합쳐지거나 덮어써질 가능성이 있다.

가장 가능성이 높은 실패 흐름은 다음과 같다.
- 서로 다른 Event 인스턴스가 같은 `EventId`를 가진다.
- 저장 시 인스턴스별 고유 식별자 없이 직렬화된다.
- 로드 시 Entry/Exit, 종류, 상호작용 객체 등의 약한 기준으로 다시 묶는다.
- 서로 다른 Event가 하나로 합쳐지거나 일부 데이터가 덮어써진다.
- 사용자는 저장 후 다시 열었을 때 Event가 생겼다 사라지거나 잘못 연결된 것으로 보게 된다.

## 요구사항

### 기능 요구사항
- 같은 Event 종류라도 서로 다른 Event 인스턴스는 반드시 분리되어야 한다.
- 저장 후 다시 로드해도 각 Event 인스턴스가 정확히 유지되어야 한다.
- 기존 JSON을 읽는 다른 툴과의 호환성을 유지해야 한다.
- 새 식별자가 없는 기존 JSON 파일도 계속 열 수 있어야 한다.
- 기존 파일을 다시 저장하면 이후부터는 안정적인 Event 식별 방식으로 동작해야 한다.

### 호환성 요구사항
- `category_id`, `track_id`, `track_info`, `interacting_object` 같은 기존 JSON 필드는 유지해야 한다.
- 필요하다면 추가 필드는 1개만 허용한다.
- 다른 툴이 알 수 없는 필드를 무시하는 경우, 계속 정상적으로 JSON을 읽을 수 있어야 한다.

### 비목표
- 외부 JSON 스키마를 대폭 바꾸지 않는다.
- Person/Vehicle 식별 규칙은 이번 단계에서 변경하지 않는다.
- Event 외 클래스의 저장 구조는 이번 단계에서 건드리지 않는다.

## 권장 접근 방식
기존 JSON 구조는 유지하고, Event 전용 보조 식별자 `event_instance_id`를 1개 추가한다.

이 필드는 다음 영역에서 동일한 Event 인스턴스를 묶는 기준이 된다.
- Event BoundingBox
- Event Waypoint
- 저장/재로드 과정
- Exit 조정, 종료, 재추적, 전파 같은 편집 동작

즉 앞으로 `EventId`는 Event 종류를 의미하는 값으로만 사용하고, 실제 개별 Event 인스턴스는 `event_instance_id`로 식별한다.

## 검토한 대안

### 대안 1: JSON 구조는 그대로 두고 내부 그룹핑만 개선
장점:
- 스키마 변경이 없다.
- 외부 호환성 측면에서 가장 안전하다.

단점:
- 로드 시 종류, Entry/Exit, 상호작용 객체, 궤적 같은 약한 단서를 조합해 인스턴스를 추론해야 한다.
- 애매한 경우를 완전히 안정적으로 복원하기 어렵다.
- 같은 문제가 다시 재발할 가능성이 높다.

### 대안 2: 기존 JSON 유지 + `event_instance_id` 추가
장점:
- 개별 Event 인스턴스를 안정적으로 식별할 수 있다.
- 기존 필드는 유지하므로 외부 영향이 작다.
- 기존 파일도 fallback 복원이 가능하다.
- 다른 툴은 새 필드를 무시해도 기존처럼 동작할 가능성이 높다.

단점:
- 스키마에 작은 확장이 생긴다.
- 레거시 파일용 복원 로직이 추가로 필요하다.

### 대안 3: Event 저장 구조를 전면 재설계
장점:
- 장기적으로 가장 깔끔하다.

단점:
- 외부 도구 영향이 크다.
- 현재 요구사항인 기존 구조 호환 우선과 맞지 않는다.

권장안은 대안 2다.

## 데이터 모델 변경

### AnnotationData
다음 선택적 필드를 추가한다.
- `event_instance_id`

동작 원칙:
- Event annotation에만 기록한다.
- Person, Vehicle에는 기록하지 않는다.
- 가능한 기본 동작으로 저장하되, 필요 시 없는 파일도 계속 읽을 수 있어야 한다.

### BoundingBox
내부 속성으로 다음 필드를 추가한다.
- `EventInstanceId`

동작 원칙:
- `Label == "event"`인 경우에만 사용한다.
- Event 박스를 복사, 전파, 보간, 재추적할 때 함께 유지되어야 한다.

### WaypointMarker
내부 속성으로 다음 필드를 추가한다.
- `EventInstanceId`

동작 원칙:
- Event Waypoint에만 사용한다.
- 툴 내부에서 Event Waypoint의 1차 식별자로 사용한다.

## 식별 규칙

### Event 종류와 Event 인스턴스의 구분
- `EventId`는 `contact`, `exchange`, `board` 같은 Event 종류를 나타낸다.
- `EventInstanceId`는 타임라인 상의 개별 Event 발생 1건을 나타낸다.

### 서로 다른 Event로 봐야 하는 기준
같은 종류의 Event라도 아래 기준 중 하나라도 실질적으로 다르면 별도 Event 인스턴스로 유지해야 한다.
- Entry/Exit 구간
- 상호작용 객체(`interacting_object`)
- bbox 위치 또는 프레임별 궤적

한 번 `EventInstanceId`가 부여된 후에는 서로 다른 Event를 다시 합치지 않는다.

## 저장 파이프라인 설계
Event annotation 저장 시 다음 순서를 따른다.
1. 각 Event BoundingBox가 어떤 Event Waypoint 또는 Event 인스턴스에 속하는지 먼저 결정한다.
2. 기존 필드인 `category_id`, `track_id`, `track_info`, `interacting_object`는 그대로 유지한다.
3. Event annotation에는 `event_instance_id`를 추가 기록한다.
4. 같은 Event 인스턴스에 속한 모든 annotation은 동일한 `event_instance_id`를 가져야 한다.
5. 저장 과정에서 `EventId`만으로 Event 인스턴스를 추론하지 않는다.

### 중요한 원칙
`track_id`는 기존 하위 도구와의 호환성을 위해 그대로 유지한다.
즉 `track_id`를 새로운 Event 인스턴스 고유 키로 재해석하지 않는다.
개별 Event 인스턴스 식별은 반드시 별도 필드 `event_instance_id`로 처리한다.

## 로드 파이프라인 설계
Event annotation 로드 시 다음 순서를 따른다.
1. `event_instance_id`가 있으면 그것을 최우선 그룹핑 키로 사용한다.
2. 같은 `event_instance_id`를 가진 Event bbox와 waypoint를 함께 복원한다.
3. `event_instance_id`가 없으면 레거시 파일로 간주한다.
4. 레거시 파일은 아래 정보를 조합해 임시 Event 그룹을 만든다.
   - Event 종류
   - track entry/exit 범위
   - interacting_object
   - bbox 연속성 및 궤적 근접성
5. 메모리에서 복원이 끝나면 각 레거시 Event 그룹에 새로운 `EventInstanceId`를 부여한다.
6. 이후 다시 저장하면 `event_instance_id`가 포함된 안정 포맷으로 승격된다.

## 메모리 일관성 규칙
Event 관련 수정은 아래 세 요소가 항상 함께 움직여야 한다.
- Event BoundingBox 집합
- Event Waypoint
- EventInstanceId 연결 정보

셋 중 하나만 갱신되는 경로는 허용하지 않는다.

### 커밋 규칙
- Entry만 찍은 상태는 아직 Event 인스턴스 확정 상태로 보지 않는다.
- Exit가 확정되는 시점에 Event 인스턴스를 확정하거나 갱신한다.
- Exit 단축, Event 종료, Event 전파, Event 재추적은 모두 `EventInstanceId`를 우선 기준으로 동작해야 한다.
- UI 선택, 리스트 렌더링, 프레임 이동도 `EventId` 단독이 아니라 `EventInstanceId` 우선으로 Event를 찾아야 한다.

## 변경 대상 코드 영역
구현 시 [Form1.cs](C:/Users/ANNA/Documents/ETRILabelingTools/WinFormsApp1/Form1.cs)에서 다음 영역을 우선 수정한다.
- Event BoundingBox 생성 및 복제 경로
- Entry/Exit 처리 시 waypoint 생성/갱신 경로
- Event 종료 로직
- Event 전파, 보간, 재추적 경로
- JSON export 시 annotation 생성 경로
- JSON load 시 temp BoundingBox / temp Waypoint 복원 경로
- `FindWaypointForBox`처럼 현재 약한 식별 규칙에 의존하는 조회 헬퍼

## 예외 처리
- 서로 호환되지 않는 Event들이 같은 `event_instance_id`를 가진 채 들어오면 경고 로그를 남기고 메모리에서 분리 재할당한다.
- Event annotation에 `track_info`가 없으면 bbox는 로드하되 waypoint 복원은 불완전 상태로 처리한다.
- 레거시 파일에서 애매한 경우, 서로 다른 Event를 공격적으로 합치기보다 분리 보존을 우선한다.
- 알 수 없는 추가 JSON 필드는 계속 안전하게 무시되어야 한다.

## 검증 계획

### 시나리오 1: 같은 Event 종류 반복
- 서로 다른 구간에 `contact` Event 2개를 만든다.
- 저장 후 다시 로드한다.
- 두 Event 인스턴스가 모두 독립적으로 유지되어야 한다.

### 시나리오 2: 같은 Event 종류 + 다른 상호작용 객체
- 같은 종류 Event 2개에 서로 다른 `interacting_object`를 설정한다.
- 저장 후 다시 로드한다.
- 각 Event가 자기 객체 정보와 waypoint를 유지해야 한다.

### 시나리오 3: Exit 조정
- 특정 Event 하나의 Exit를 줄인다.
- 저장 후 다시 로드한다.
- 해당 Event만 범위가 바뀌고, 다른 동일 종류 Event는 그대로여야 한다.

### 시나리오 4: 레거시 JSON
- `event_instance_id`가 없는 기존 파일을 연다.
- Event가 정상 표시되는지 확인한다.
- 다시 저장 후 재로드한다.
- 이후부터는 Event가 안정적으로 유지되어야 한다.

## 위험 요소
- 과거 파일 자체가 이미 애매하게 저장되어 있다면 레거시 복원에 경계 사례가 남을 수 있다.
- 일부 후속 툴이 엄격한 스키마 검증을 하면 새 필드를 허용하지 않을 수 있다.
- 코드 일부가 여전히 `EventId`만 기준으로 Event를 찾으면 같은 문제가 부분적으로 남을 수 있다.

## 완화 방안
- 새 필드는 선택적이고 비파괴적으로 추가한다.
- 후속 툴 담당자에게 `event_instance_id` 의미를 명확히 문서로 전달한다.
- Event 조회 로직을 공용 헬퍼로 모아 식별 규칙을 한 곳에서 관리한다.
- 애매한 경우에는 잘못 합치는 것보다 과하게 분리 보존하는 쪽을 택한다.

## 적용 방침
- 새로 저장하는 파일에는 기본적으로 `event_instance_id`를 기록한다.
- 수동 마이그레이션 단계는 두지 않는다.
- 기존 파일은 처음 다시 저장될 때 자연스럽게 새 방식으로 승격된다.

## 최종 결정
이번 설계에서는 대안 2를 채택한다.
- 기존 JSON 구조는 유지한다.
- Event 전용 선택적 필드 `event_instance_id`를 1개 추가한다.
- 툴 내부와 저장/재로드 과정 모두에서 이 값을 Event 인스턴스의 안정 식별자로 사용한다.