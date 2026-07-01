# JSON/Model Separation Design

## Goal

`WinFormsApp1/Form1.cs`에 몰려 있는 데이터 모델과 JSON 직렬화용 타입 선언을 별도 `Models` 폴더로 분리해, 화면 로직과 데이터 정의의 경계를 명확히 만든다.

이번 단계의 목적은 **구조 개선**이며, 기능 추가나 동작 변경이 아니다. 특히 `C:\Users\ANNA\AOLTv1.0`의 기능 구현을 가져오지 않고, 폴더 분리 방식과 책임 경계만 참고한다.

## Non-Goals

- JSON load/export 동작 자체를 `JsonService`로 이동하지 않는다.
- YOLO/OpenCV 추적 로직을 분리하지 않는다.
- UI 이벤트 처리 구조를 바꾸지 않는다.
- face/plate subordinate box 기능의 동작을 변경하지 않는다.

## Reference Constraint

참고 프로젝트 `C:\Users\ANNA\AOLTv1.0`에서 차용하는 것은 아래뿐이다.

- `Forms / Models / Services / Helpers` 식의 역할 분리 방식
- 데이터 구조를 화면 클래스 밖으로 분리하는 설계 방향

차용하지 않는 것은 아래다.

- 라벨링 동작 방식
- 저장 포맷 세부 설계
- 추적 로직 구현
- 보안/로그/설정 기능

즉, 이번 작업은 “기능 이식”이 아니라 “현재 코드의 책임 재배치”다.

## Current Problem

현재 [Form1.cs](C:\Users\ANNA\Documents\ETRILabelingTools\WinFormsApp1\Form1.cs)에 아래가 함께 들어 있다.

- UI 이벤트 처리
- 렌더링 로직
- 비디오 제어
- 추적 로직
- JSON 로드/저장 로직
- 도메인 모델 선언
- JSON DTO 선언

이 구조는 다음 문제를 만든다.

- 파일 상단 타입 선언 수정이 곧바로 대형 화면 파일 전체 리스크로 이어진다.
- 모델 변경과 UI 변경의 diff가 섞여 리뷰와 디버깅이 어려워진다.
- 이후 `JsonService`, `TrackingService`, `CoordinateHelper` 같은 추가 분리를 진행하기 어렵다.

## Design Summary

이번 단계에서는 **모델과 JSON 타입만 먼저 이동**한다.

핵심 원칙은 아래와 같다.

- `Form1`은 계속 메인 오케스트레이터 역할을 맡는다.
- 타입 정의만 외부 파일로 이동하고, 호출 구조와 상태 흐름은 유지한다.
- namespace 정리와 `using` 추가만으로 기존 동작이 유지되게 한다.
- 이후 단계의 서비스 분리를 위한 안정적인 경계를 먼저 만든다.

## Target Structure

추가할 구조는 아래와 같다.

```text
WinFormsApp1/
  Models/
    Annotation/
      BoundingBox.cs
      WaypointMarker.cs
      CustomLabel.cs
    Json/
      LabelingDataModels.cs
      LegacyJsonModels.cs
```

필요 시 `SubtitleEntry`는 `Annotation` 또는 `Common` 성격으로 둘 수 있으나, 이번 단계에서는 `BoundingBox` 인접 모델과 함께 분리하는 쪽을 우선한다.

## File Responsibilities

### `Models/Annotation/BoundingBox.cs`

포함 대상:

- `BoundingBox`
- subordinate face/plate 관련 필드
- skeleton / person attributes 관련 필드

이 파일은 화면이 아니라 “프레임 단위 라벨 박스 데이터”를 표현한다.

### `Models/Annotation/WaypointMarker.cs`

포함 대상:

- `WaypointMarker`

이 파일은 entry/exit 구간과 객체 식별 정보를 가지는 waypoint 모델만 담당한다.

### `Models/Annotation/CustomLabel.cs`

포함 대상:

- `CustomLabel`

완전한 도메인 모델은 아니지만, `Form1` 내부 타입 선언에서 제거해 UI 보조 모델로 독립시킨다.

### `Models/Json/LabelingDataModels.cs`

포함 대상:

- `ImageInfo`
- `TrackEntry`
- `TrackInfo`
- `AnnotationData`
- `CategoryData`
- `VideoInfoExtended`
- `LabelingDataExtended`

이 파일은 JSON 직렬화에 직접 대응되는 타입만 모은다.

### `Models/Json/LegacyJsonModels.cs`

포함 대상:

- 현재 `Legacy JSON Classes` region에 있는 호환성 유지 타입

이 파일은 레거시 포맷 호환 전용 타입을 분리해 현재 포맷 타입과 섞이지 않게 한다.

## Form1 After Refactor

[Form1.cs](C:\Users\ANNA\Documents\ETRILabelingTools\WinFormsApp1\Form1.cs)는 아래 역할만 직접 가진다.

- WinForms 화면 이벤트 처리
- 앱 상태 관리
- 모델 인스턴스 조합
- JSON load/export 호출부
- 추적/렌더링/좌표 처리의 실제 실행 흐름

반대로 아래는 제거된다.

- 상단의 모델 선언 블록
- JSON DTO 선언 블록
- legacy JSON 타입 선언 블록

이렇게 하면 `Form1`의 구조는 바꾸지 않으면서 파일의 책임 밀도를 줄일 수 있다.

## Compatibility Requirements

이번 분리는 반드시 아래 조건을 만족해야 한다.

- 기존 JSON 파일과의 하위호환 동작이 바뀌지 않아야 한다.
- 최근 추가한 subordinate face/plate 필드가 그대로 유지되어야 한다.
- `dotnet build ETRILabelingTool.sln` 기준 빌드가 성공해야 한다.
- 앱 실행 시 초기 로딩 단계에서 타입 이동으로 인한 예외가 없어야 한다.

## Migration Strategy

작업 순서는 아래로 고정한다.

1. `Models` 폴더와 대상 파일을 생성한다.
2. `Form1.cs` 상단 타입 선언을 새 파일로 이동한다.
3. `namespace WinFormsApp1`을 유지해 참조 변경을 최소화한다.
4. 필요한 `using`만 추가한다.
5. `Form1.cs`의 기존 타입 선언을 제거한다.
6. 빌드로 즉시 검증한다.
7. 앱 실행으로 최소 동작 검증을 한다.

## Risk Analysis

주요 리스크는 아래와 같다.

- partial namespace/using 누락으로 인한 컴파일 실패
- legacy JSON 타입이 이동되면서 deserialize 경로가 깨지는 문제
- subordinate box 관련 새 필드가 누락되는 문제
- 같은 이름 타입이 중복 정의되어 충돌하는 문제

이를 줄이기 위한 대응은 아래와 같다.

- 타입 이동은 로직 수정 없이 “정의 복사 후 원본 제거” 순서로 진행한다.
- 메서드 분리와 타입 이동을 한 번에 하지 않는다.
- 새 파일 추가 직후 바로 빌드한다.

## Rejected Alternatives

### 모델과 `JsonService`를 한 번에 분리

장점은 더 큰 구조 개선이지만, 현재 `Form1` 상태 의존성이 높아 이번 단계 리스크가 커진다.

### DTO와 도메인 모델을 완전히 분리

장기적으로는 바람직하지만, 지금은 mapping 계층까지 만들면 범위가 커져 기능 안정성을 해칠 가능성이 있다.

### `AOLTv1.0` 구조를 더 직접적으로 따라가기

폴더명과 역할 분리는 참고 가능하지만, 기능이나 구체 구현을 그대로 맞추는 것은 이번 요구사항에 어긋난다.

## Next Step Boundary

이번 설계가 끝난 뒤 다음 구현 단계는 오직 아래만 포함한다.

- 모델/JSON 타입 파일 생성
- `Form1.cs`에서 타입 정의 제거
- namespace/using 정리
- 빌드 및 최소 실행 검증

그 다음 단계 후보는 아래다.

- `JsonService` 분리
- `TrackingService` 분리
- `CoordinateHelper` / 렌더링 보조 분리

## Acceptance Criteria

- [Form1.cs](C:\Users\ANNA\Documents\ETRILabelingTools\WinFormsApp1\Form1.cs)에서 모델/DTO 선언부가 제거된다.
- `Models/Annotation` 및 `Models/Json`에 타입이 재배치된다.
- subordinate face/plate 필드가 모두 유지된다.
- legacy JSON 호환 타입이 별도 파일로 유지된다.
- `dotnet build ETRILabelingTool.sln`이 성공한다.
- 앱이 시작 직후 크래시하지 않는다.
