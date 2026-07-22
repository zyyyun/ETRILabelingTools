---
status: partial
phase: 04-reproduce-ppt-scenarios-and-lock-in-regression-coverage
source:
  - 04-PPT-VERIFICATION.md
started: 2026-07-22T10:30:00+09:00
updated: 2026-07-22T10:30:00+09:00
---

# Phase 4 수동 / 하이브리드 검증 로그

## 현재 상태

수동 또는 하이브리드로 분류된 PPT 시나리오의 실제 재현 검증을 기다리는 상태입니다.

## 검증 항목

### 1. PPT 1 / G1
expected: Event finalization creates only one event segment.
result: pending
note: event가 2개로 갈라지지 않는지 직접 확인

### 2. PPT 2 / G5
expected: Event interacting-object editor opens on the object column, not the exit column.
result: pending
note: event 리스트 인라인 편집 컬럼 위치 확인

### 3. PPT 3 / G2
expected: Event boxes disappear immediately after ExitFrame.
result: pending
note: exit 직후 프레임에서 event box가 사라지는지 확인

### 4. PPT 4 / G1 + G2
expected: `E/X` event flow creates one event and event lifetime stays bounded.
result: pending
note: 보간/자동 추적 사용 여부에 따른 차이도 함께 기록

### 5. PPT 5 / G3
expected: Vehicle overlap alone does not trigger extra event-side vehicle behavior.
result: pending
note: overlap 자체가 확인 메시지/side effect 조건이 되는지 확인

### 6. PPT 6 / G1
expected: No exit-frame bbox path still creates a single event waypoint.
result: pending
note: 현재 정상 동작 기준선 케이스

### 7. PPT 7 / G4
expected: Frame-level delete removes the related undeletable event waypoint artifacts.
result: pending
note: 삭제 경로 후 잔여 event waypoint 존재 여부 확인

### 8. PPT 8 / G2 + G4
expected: Event does not persist until the overlapped object exit and can be cleaned up.
result: pending
note: AOL 스타일 수동 추적 기준과 비교 메모 남기기

### 9. PPT 9 / G5
expected: Vehicle/event deletion does not delete a stale selected person waypoint.
result: pending
note: 하단 빈 공간 클릭 해제 동작도 함께 확인

### 10. PPT 10 / G3
expected: Exit-frame bbox + auto-track does not duplicate vehicle entry-frame boxes.
result: pending
note: vehicle 클래스에만 나타나는지 여부도 기록

### 11. PPT 11 / G3
expected: One logical vehicle does not split into `01` and `02`.
result: pending
note: entry와 exit에서 vehicle label 표기 비교

### 12. PPT 12 / G1 + G3
expected: Exit-frame bbox + auto-track does not create duplicate event and vehicle artifacts.
result: pending
note: event 중복과 vehicle 중복을 동시에 판정

### 13. PPT 12-2 / G3
expected: Motorcycle/bicycle variants do not duplicate the same logical object.
result: pending
note: YOLO가 약한 클래스에서 첫 프레임 고정 중복이 남는지 확인

### 14. PPT 13 / G4
expected: No ghost `contact` waypoint appears and persists after the event flow.
result: pending
note: person 01, 02가 사라질 때까지 유지되는지 여부 확인

## 요약

total: 14
passed: 0
issues: 0
pending: 14
skipped: 0
blocked: 0

## 이슈 메모

아직 없음. 수동 재현 후 기록합니다.
