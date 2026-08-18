const state = { running: false, sequence: 0, taskId: "", lastError: "" };
const $ = id => document.getElementById(id);

function render() {
  $("status").textContent = state.running ? "Rerun 실행 중" : "Rerun 정지";
  $("details").textContent = JSON.stringify(state, null, 2);
}

async function sendToChatGPT(prompt) {
  try {
    const result = await chrome.runtime.sendMessage({ type: "send_prompt", prompt });
    if (!result?.ok) throw new Error(result?.error || "메시지 전송 실패");
    state.lastError = "";
    return result;
  } catch (error) {
    state.lastError = error?.message || String(error);
    render();
    throw error;
  }
}

$("start").onclick = async () => {
  state.running = true;
  state.sequence = 0;
  state.taskId = "corridor-employee-movement";
  render();
  try {
    await sendToChatGPT(`CompanyGame Rerun 시작.

현재 작업 범위는 게임적인 부분만이다.
목표: 통로(Node)와 직원 이동 시스템을 완성한다.

요구사항:
- 기존에 검증된 기능은 반복하지 않는다.
- 통로와 Node는 확장 가능한 소프트코딩 구조로 유지한다.
- 직원 선택 → 목적지 지정 → Node 경로 탐색 → Corridor를 따라 실제 이동 → 목적지 도착까지 구현한다.
- 로보토미 코퍼레이션의 직원 선택/이동 UX를 적극적으로 벤치마킹한다.
- 이동속도, 가속/감속, 도착 거리, 그룹 간격 등은 설정 데이터로 분리한다.
- result.json과 error.json을 직접 확인하고 오류가 있으면 원인을 분석하여 수정한다.
- Unity에서 실제 직원이 연결된 Node를 순서대로 따라 이동하는 것을 검증해야 완료다.
- 완료 조건을 모두 만족하면 COMPLETE 상태를 명확하게 보고한다.

먼저 현재 GitHub 프로젝트 상태와 관련 코드, result/error JSON을 확인하고 가장 작은 검증 가능한 미완료 작업부터 진행해.`);
  } catch (_) {}
};

$("resume").onclick = async () => {
  state.running = true;
  state.sequence += 1;
  render();
  try {
    await sendToChatGPT(`continue.
현재 CompanyGame의 통로(Node) + 직원 이동 작업을 이어서 진행해.
GitHub의 현재 코드와 result.json/error.json을 먼저 확인하고 검증된 작업은 반복하지 마.
실제 직원이 Node Graph를 따라 목적지까지 이동하는 것이 최종 완료 조건이다.
오류가 있으면 원인을 직접 확인하고 수정한 뒤 다시 검증해.`);
  } catch (_) {}
};

$("stop").onclick = () => {
  state.running = false;
  render();
};

render();
