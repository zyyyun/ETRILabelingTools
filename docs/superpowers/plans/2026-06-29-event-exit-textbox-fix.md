# Event Exit 셀이 텍스트 필드로 보이는 현상 수정

## 원인
항목 1 작업에서 Event waypoint ListView 컬럼을 3개에서 4개로 변경했다.
- 이전: `Event(0) | timestamp(1) | 객체(2)` — 컬럼 2가 객체
- 이후: `Event(0) | Entry(1) | Exit(2) | 객체(3)` — 컬럼 2가 Exit, 컬럼 3이 객체

하지만 기존 인라인 편집 로직이 **컬럼 인덱스 2를 "객체 컬럼"으로 하드코딩**하고 있어,
변경 후 컬럼 2가 된 **Exit 셀 위에 편집용 TextBox(`eventListEditBox`)가 떠서**
Exit가 텍스트 필드처럼 보이는 현상이 발생했다.

person/vehicle은 이런 인라인 TextBox 편집이 없으므로 정상 표시됨。

## 수정 대상
[Form1.cs](C:/Users/ANNA/Documents/ETRILabelingTools/WinFormsApp1/Form1.cs)

### 1. `listViewEventWaypoints_DoubleClick` (L4346)
- `if (subIndex != 2) return;` → `if (subIndex != 3) return;`
- 객체 컬럼이 3번으로 이동했으므로 인덱스 수정

### 2. `listViewEventWaypoints_MouseUp` (L4411)
- `if (subIndex != 2) return;` → `if (subIndex != 3) return;`
- 동일한 이유

### 3. `CommitEventListEdit` (L4387~4390)
- `if (item.SubItems.Count >= 3) item.SubItems[2].Text = ...` → `[3]`
- 객체 텍스트를 Exit(2)가 아닌 객체(3) 컬럼에 갱신하도록 수정

## 검증
- Exit 셀 클릭 시 텍스트 필드가 나타나지 않고 Exit 프레임으로 Jump
- 객체 컬럼(4번째) 더블클릭/클릭 시에만 인라인 편집 박스 동작
- 빌드 CS 에러 0개
