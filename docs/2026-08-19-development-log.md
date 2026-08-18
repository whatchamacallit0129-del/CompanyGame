# 2026-08-19 개발 기록 — Rerun / 직원 이동 시스템

## 오늘 한 일

### 1. 게임 개발 목표 정리
- 현재 개발 범위는 게임적인 부분만 유지.
- 핵심 목표는 **통로(Corridor) + Node + 직원 이동 시스템**.
- 조작감은 **로보토미 코퍼레이션과 유사한 방향**으로 벤치마킹.
- 직원 선택 → 목적지 지정 → Node 경로 탐색 → Corridor를 따라 실제 이동 → 목적지 도착의 흐름을 목표로 함.
- 이동속도, 가속/감속, 도착 거리, 그룹 간격 등은 설정 데이터로 분리하는 소프트코딩 구조를 유지.

### 2. 현재 Unity 이동 시스템에 만들어진 것
- 직원 이동 컴포넌트가 존재함.
- Node 기반 Navigation Graph / Path 구조를 사용함.
- 이동 관련 설정을 별도 데이터로 분리한 구조를 사용함.
- 경로 탐색 안정성을 높이기 위해 NavigationService의 경로 복원 및 MovementCost 처리를 보강함.
- 직원 이동을 실제로 검증하기 위한 **Employee Movement Smoke Test**와 Unity 메뉴 테스트 명령을 추가함.
- 테스트 목적은 연결된 Node들을 순서대로 따라 실제 목적지까지 이동하는지 확인하는 것.

### 3. 실제 오류 확인 및 수정
- `results/error.json`에서 `command.json`이 다른 프로세스에서 사용 중일 때 발생하는 `IOException`을 확인함.
- `CompanyGameCommandAgent`가 파일 잠금 상황에서 즉시 실패하지 않고 재시도하도록 수정함.
- 이동 Smoke Test의 문제도 발견함. 기존 테스트가 직원에게 실제 `MoveTo()`를 실행하지 않을 가능성이 있어 실제 Play Mode에서 이동 명령을 실행하도록 보완함.

## 오늘 확인한 가장 중요한 문제 — Rerun

Rerun Chrome 확장 프로그램은 여러 번 테스트했지만 **아직 자동화가 제대로 작동하지 않음**.

현재 원하는 동작은:

`Start 1회` → ChatGPT에 작업 명령 자동 입력 → 자동 전송 → ChatGPT 응답 감지 → 다음 작업 자동 입력/전송 → 계속 반복 → 실제 `COMPLETE` 검증 시 자동 종료

그런데 현재는 사용자가 직접 `CONTINUE`를 눌러야 다음 단계가 진행되는 상황이 반복됨.

Rerun 상태에서 다음과 같은 문제가 발생했음:

```json
{
  "running": false,
  "sequence": 3,
  "taskId": "corridor-employee-movement",
  "status": "blocked",
  "lastError": "새 ChatGPT 응답을 기다리는 시간이 초과되었습니다.",
  "lastResponse": "",
  "lastResponseAt": 0,
  "startedAt": 1787077203090,
  "targetTabId": 1809922114
}
```

`targetTabId`가 잡히는 것으로 보아 ChatGPT 탭 식별까지는 되지만, **ChatGPT에 메시지를 자동 입력/전송하거나 새 Assistant 응답을 자동 감지하는 부분이 아직 신뢰할 수 없음**.

따라서 현재 Rerun은 약 10회에 가까운 시도에도 원하는 자동 작업 루프를 완성하지 못한 상태임.

### Rerun에 대해 내일 다시 볼 것
- 확장 프로그램을 부분 수정하는 수준을 계속 유지할지, 아니면 **Rerun Controller를 처음부터 단순하고 확실한 구조로 다시 만드는 것**을 검토.
- 핵심 테스트를 먼저 `자동 입력 → 자동 전송 → 응답 읽기` 하나로 단순화.
- 그 통신이 확실히 성공한 뒤 `CONTINUE` 자동 전송 루프를 붙임.
- 사용자 클릭 없이 한 번의 Start로 계속 작업할 수 있어야 함.
- 실제 COMPLETE가 확인될 때만 자동 종료해야 함.

## Unity / result.json / error.json 관련 중요한 발견

Rerun이 GitHub의 `result.json` / `error.json`을 읽는 것만으로는 충분하지 않음.

현재 구조에서는 **Unity Editor/Unity 프로젝트가 실제로 컴파일·실행되어야 최신 결과와 오류 JSON이 생성/갱신될 수 있음**.

즉:

`Rerun → GitHub 상태 확인`

만으로는 최신 Unity 실행 결과를 얻을 수 없고,

`Unity가 실제로 열려 있고 필요한 프로젝트/Editor 상태가 활성화됨`
→ Unity 컴파일/실행
→ `results/result.json`, `results/error.json` 갱신
→ GitHub 반영
→ ChatGPT가 JSON 확인

이라는 현실적인 검증 경로가 필요함.

**내일은 Rerun 자동화와 Unity 검증 경로를 분리해서 설계하는 것이 중요함.**

## 현재 개발 상태

- 직원 이동 시스템: **미완료**
- Node 경로 탐색 구조: 구현 및 보강됨
- 실제 Unity에서 직원이 연결된 Node를 순서대로 따라 목적지까지 이동했다는 검증: **아직 없음**
- `result.json`으로 이동 성공을 확인한 상태: **아님**
- Rerun 자동화: **미완료 / 재설계 검토 필요**

## 내일 시작할 때

1. 이 문서를 먼저 읽는다.
2. Rerun 자동 입력/전송/응답 감지 문제를 먼저 확인한다.
3. Rerun이 정상적으로 자동 메시지를 주고받을 수 있게 만든다.
4. Unity의 실제 컴파일/실행 결과를 JSON으로 가져오는 경로를 확인한다.
5. 그 다음 직원 Node 이동 검증을 이어간다.

오늘은 여기까지. 직원 이동 시스템을 성급하게 COMPLETE 처리하지 않는다.
