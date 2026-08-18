const state = { running: false, sequence: 0, taskId: "", lastError: "" };
const $ = id => document.getElementById(id);

function render() {
  $("status").textContent = state.running ? "Rerun 실행 중" : "Rerun 정지";
  $("details").textContent = JSON.stringify(state, null, 2);
}

async function getActiveChatTab() {
  const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
  return tabs.find(t => t.url?.includes("chatgpt.com"));
}

async function sendToChatGPT(prompt) {
  let tab = await getActiveChatTab();
  if (!tab) {
    const result = await chrome.runtime.sendMessage({ type: "open_chatgpt" });
    tab = { id: result.tabId };
    await new Promise(r => setTimeout(r, 1200));
  }
  return chrome.tabs.sendMessage(tab.id, { type: "send_prompt", prompt });
}

$("start").onclick = async () => {
  state.running = true;
  render();
  await sendToChatGPT(`CompanyGame Rerun 시작. 현재 작업 상태를 확인하고, 기존에 검증된 작업은 반복하지 마. result.json과 error.json을 직접 읽어 오류가 있으면 원인을 분석하고 수정해. 현재 프로젝트 목표는 통로(Node)와 직원 이동 시스템을 소프트코딩으로 확장 가능하게 만드는 것이다. 먼저 현재 상태를 확인하고 가장 작은 검증 가능한 작업부터 진행해.`);
};

$("resume").onclick = async () => {
  state.running = true;
  render();
  await sendToChatGPT(`continue. 현재 CompanyGame 작업을 이어서 진행해. GitHub의 현재 상태와 result/error JSON을 확인하고 미완료 작업부터 재개해.`);
};

$("stop").onclick = () => {
  state.running = false;
  render();
};

render();
