# 개발 진행 상황 — 2026-08-18

## 현재 완료된 구조

현재 Unity 자동화의 기본 단방향 파이프라인이 정상 작동한다.

```text
AI 클라이언트
      ↓
Company MCP (mcp_server.py)
      ↓
CompanyChatRelay (relay_server.py)
      ↓
D:\CompanyProject\command.json
      ↓
CompanyGameCommandAgent
      ↓
Unity Editor
```

## CompanyChatRelay

- 로컬 HTTP 서버: `127.0.0.1:8765`
- `/status`: Relay 상태 확인
- `/unity`: Unity 명령을 `D:\CompanyProject\command.json`에 기록
- `/codex`: 설치된 Codex CLI 실행 기능이 있으나, 현재 상태에서는 `codex_cli_found: false`였음

## MCP Adapter

`D:\CompanyProject\CompanyChatRelay\mcp_server.py`

STDIO MCP 서버이며 현재 Tool은 다음과 같다.

- `get_relay_status`
- `run_codex`
- `queue_unity_command`

Cursor MCP 연결은 이미 완료했고 실제 Unity 명령 테스트까지 성공했다. **앞으로 Cursor 연결 작업을 다시 할 필요는 없다.** Cursor는 이 MCP를 사용할 수 있는 클라이언트 중 하나일 뿐이다.

## Python 환경

- `D:\CompanyProject\CompanyChatRelay\.venv\` 생성 완료
- MCP 패키지 설치 및 `MCPServer OK` 확인 완료
- 잘못 `C:\Users\jihwan\.venv`에 만들었던 환경은 삭제함

## Unity Command Agent

`Assets/Editor/CompanyGameCommandAgent.cs`가 Unity Editor에서 `command.json`을 감시한다.

현재 기본적인 GameObject 생성/삭제 및 Transform/component 관련 명령을 지원하며, 앞으로 명령을 크게 확장할 예정이다.

핵심 동작:

1. `command.json` 발견
2. 명령 문자열 읽기
3. 지원되는 명령 실행
4. 성공하면 `command.json` 삭제
5. `AssetDatabase.Refresh()` 실행

## 기존 GitHub ↔ Unity 자동화 루프

이미 구축한 GitHub 자동 Pull/Push 루프를 활용한다.

목표 흐름:

```text
GitHub
 ↓
자동 Pull
 ↓
CompanyProject
 ↓
Unity Command Agent
 ↓
Unity 변경
 ↓
자동 Commit / Push
 ↓
GitHub
```

따라서 새로운 양방향 MCP 시스템을 당장 처음부터 만들기보다, 기존 GitHub Pull/Push 루프를 활용해 Unity 결과를 GitHub 변경사항으로 확인하는 방향을 우선 검토한다.

## 앞으로의 핵심 개발 목표

### 1. Command Agent 대폭 확장

현재 제한된 명령을 게임 개발에 필요한 수준까지 확장한다.

예정 명령 예시:

- GameObject 생성/삭제/이름 변경
- Transform 변경
- Component 추가/삭제
- Parent/Child 설정
- Prefab 생성/저장
- Scene 저장/로드
- GameObject 활성화/비활성화
- Tag/Layer 설정
- Sprite/Text 설정
- Script 관련 작업

### 2. GitHub를 코드 개발/검증의 중심으로 사용

Command Agent 자체를 AI처럼 만들기보다, 실제 C# 소스코드는 GitHub에서 관리한다.

목표:

```text
AI
 ↓
GitHub CompanyGame 코드 확인/수정
 ↓
기존 자동 Pull
 ↓
Unity 컴파일/실행
 ↓
기존 자동 Commit/Push
 ↓
AI가 GitHub에서 결과 확인
```

즉, AI가 GitHub에서 Unity 프로젝트의 코드를 확인하고 필요한 Command Agent 코드와 게임 코드를 수정한 뒤, 기존 자동화 루프를 통해 Unity에 적용하는 방식이다.

### 3. 양방향 MCP는 필요성을 보고 결정

별도의 `result.json` 같은 Unity → MCP 결과 통신도 고려했지만, 기존 GitHub Pull/Push 루프로 결과를 확인할 수 있다면 당장은 우선순위를 낮춘다.

필요해질 경우 이후 Unity Console/컴파일 오류/Hierarchy 등의 실시간 결과 전달을 추가한다.

## 개발 원칙

- 현재 정상 작동하는 단방향 파이프라인은 먼저 보존한다.
- 이미 완료된 Cursor MCP 연결을 다시 작업하지 않는다.
- Command Agent를 단계적으로 확장한다.
- 실제 게임 개발 전에 자동화 기반을 충분히 테스트한다.
- 코드는 GitHub에서 버전 관리한다.
- Unity가 실제로 실행한 결과를 GitHub 변경사항과 컴파일 결과로 확인한 뒤 다음 작업으로 넘어간다.

## 다음 작업 재개 지점

1. 기존 GitHub ↔ Unity 자동 Pull/Push 루프가 어떤 파일/BAT로 구성되어 있는지 확인
2. 현재 `CompanyGameCommandAgent.cs`의 GitHub 최신 상태 확인
3. Command Agent 명령을 게임 개발에 필요한 수준으로 확장
4. 확장된 명령을 하나씩 Unity에서 테스트
5. GitHub → Unity → GitHub 루프가 각 명령에서 정상 작동하는지 확인
6. 이후 실제 회사 경영 게임 시스템 개발 시작
