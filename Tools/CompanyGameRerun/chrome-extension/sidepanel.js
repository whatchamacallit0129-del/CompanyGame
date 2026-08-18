const STORAGE_KEY = "companygame_rerun_state";
const state = {
  running: false,
  sequence: 0,
  taskId: "corridor-employee-movement",
  status: "idle",
  lastError: ""
};
const $ = id => document.getElementById(id);

async function loadState() {
  const saved = await chrome.storage.local.get(STORAGE_KEY);
  if (saved[STORAGE_KEY]) Object.assign(state, saved[STORAGE_KEY]);
  render();
}

async function saveState() {
  await chrome.storage.local.set({ [STORAGE_KEY]: { ...state } });
}

function render() {
  $("status").textContent = state.running ? "Rerun 실행 중" : "Rerun 정지";
  $("details").textContent = JSON.stringify(state, null, 2);
}

async function sendToChatGPT(prompt) {
  try {
    const result = await chrome.runtime.sendMessage({ type: "send_prompt", prompt });
    if (!result?.ok) throw new Error(result?.error || "메시지 전송 실패");
    state.lastError = "";
    await saveState();
    render();
    return result;
  } catch (error) {
    state.lastError = error?.message || String(error);
    state.status = "error";
    await saveState();
    render();
    throw error;
  }
}

$("start").onclick = async () => {
  // Start = 새 작업 authorization. 이전 sequence를 이어가지 않는다.
  state.running = true;
  state.sequence += 1;
  state.taskId = "corridor-employee-movement";
  state.status = "continue";
  state.lastError = "";
  await saveState();
  render();

  try {
    await sendToChatGPT(`START — CompanyGame Rerun 새 작업 authorization.

현재 작업 범위는 게임적인 부분만이다.
Task ID: corridor-employee-movement
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
  // Continue = 같은 task/sequence의 work start 또는 resume authorization.
  // Stop 후에도 sequence와 task를 유지한다.
  state.running = true;
  state.status = "continue";
  state.lastError = "";
  await saveState();
  render();

  try {
    await sendToChatGPT(`CONTINUE — 현재 CompanyGame Rerun 작업 authorization을 다시 시작한다.

같은 Task ID(${state.taskId}), sequence(${state.sequence})를 유지한다.
현재 GitHub의 상태와 result.json/error.json을 먼저 확인하고 현재 sequence의 미완료 지점부터 이어서 진행해.
검증된 작업은 반복하지 마.

최종 완료 조건은 실제 직원이 연결된 Node Graph를 따라 목적지까지 이동하는 것의 검증이다.
오류가 있으면 원인을 직접 확인하고 수정한 뒤 다시 검증한다.
모든 acceptance criteria가 실제로 충족되면 COMPLETE라고 명확하게 보고한다.`);
  } catch (_) {}
};

$("stop").onclick = async () => {
  // Stop = watcher/작업 authorization 중지. task/sequence는 보존한다.
  state.running = false;
  state.status = "stopped";
  await saveState();
  render();
};

loadState();
