const CHATGPT_URL = "https://chatgpt.com/";
const CHATGPT_HOSTS = ["chatgpt.com", "www.chatgpt.com"];
const STORAGE_KEY = "companygame_rerun_state";
const DEFAULT_STATE = { running:false, sequence:0, taskId:"", status:"stopped", lastError:"", lastResponse:"", lastResponseAt:0, startedAt:0, targetTabId:0 };
const HARD_STOP_MS = 20 * 60 * 1000;
const CHECKPOINT_MS = 18 * 60 * 1000;
let dispatching = false;
let checkpointTimer = null;
let hardStopTimer = null;

chrome.runtime.onInstalled.addListener(async () => {
  await chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {});
  const current = await chrome.storage.local.get(STORAGE_KEY);
  if (!current[STORAGE_KEY]) await chrome.storage.local.set({ [STORAGE_KEY]: DEFAULT_STATE });
});

function isChatGPTTab(tab) {
  return !!tab?.url && CHATGPT_HOSTS.some(host => tab.url.startsWith(`https://${host}/`));
}
async function getState() {
  const data = await chrome.storage.local.get(STORAGE_KEY);
  return { ...DEFAULT_STATE, ...(data[STORAGE_KEY] || {}) };
}
async function setState(patch) {
  const state = { ...(await getState()), ...patch };
  await chrome.storage.local.set({ [STORAGE_KEY]: state });
  return state;
}

async function resolveTargetTab() {
  const state = await getState();
  if (state.targetTabId) {
    try {
      const tab = await chrome.tabs.get(state.targetTabId);
      if (isChatGPTTab(tab)) return tab.id;
    } catch (_) {}
  }

  const tabs = await chrome.tabs.query({ active:true, lastFocusedWindow:true });
  const active = tabs.find(isChatGPTTab);
  if (active?.id) return active.id;

  const all = await chrome.tabs.query({});
  const existing = all.find(isChatGPTTab);
  if (existing?.id) return existing.id;

  throw new Error("현재 열려 있는 ChatGPT 대화를 찾지 못했습니다. ChatGPT 작업 대화를 먼저 열어주세요.");
}

async function ensureContentScript(tabId) {
  try {
    const response = await chrome.tabs.sendMessage(tabId, { type:"ping" });
    if (response?.ok) return;
  } catch (_) {}
  await chrome.scripting.executeScript({ target:{ tabId }, files:["content.js"] });
  await new Promise(resolve => setTimeout(resolve, 500));
  const response = await chrome.tabs.sendMessage(tabId, { type:"ping" });
  if (!response?.ok) throw new Error("현재 ChatGPT 대화에 content script를 연결하지 못했습니다.");
}

async function sendPrompt(prompt) {
  const tabId = await resolveTargetTab();
  await ensureContentScript(tabId);
  await setState({ targetTabId:tabId });
  const result = await chrome.tabs.sendMessage(tabId, { type:"send_prompt", prompt });
  if (!result?.ok) throw new Error(result?.error || "현재 ChatGPT 대화로 메시지를 전송하지 못했습니다.");
  return { ok:true, tabId };
}

function clearRunTimers() {
  if (checkpointTimer) clearTimeout(checkpointTimer);
  if (hardStopTimer) clearTimeout(hardStopTimer);
  checkpointTimer = null;
  hardStopTimer = null;
}
async function stopRun(status="stopped", error="") {
  clearRunTimers();
  await setState({ running:false, status, lastError:error });
}

async function dispatchNext(reason="CONTINUE") {
  if (dispatching) return;
  dispatching = true;
  try {
    const state = await getState();
    if (!state.running || state.status !== "continue") return;
    if (Date.now() - state.startedAt >= HARD_STOP_MS) {
      await stopRun("blocked", "20분 hard stop에 도달했습니다.");
      return;
    }

    const nextSequence = state.sequence + 1;
    const prompt = `${reason} — CompanyGame Rerun 작업 authorization.
현재 작업 범위는 게임적인 부분만이다.
Task ID: ${state.taskId}
Sequence: ${nextSequence}

목표: 통로(Node)와 직원 이동 시스템을 로보토미 코퍼레이션과 유사한 조작감으로 완성한다.

요구사항:
- 기존에 검증된 기능은 반복하지 않는다.
- 통로와 Node는 확장 가능한 소프트코딩 구조로 유지한다.
- 직원 선택 → 목적지 지정 → Node 경로 탐색 → Corridor를 따라 실제 이동 → 목적지 도착까지 구현한다.
- 이동속도, 가속/감속, 도착 거리, 그룹 간격 등은 설정 데이터로 분리한다.
- result.json과 error.json을 직접 확인하고 오류가 있으면 원인을 분석하여 수정한다.
- Unity에서 실제 직원이 연결된 Node를 순서대로 따라 목적지까지 이동하는 것을 검증하기 전에는 COMPLETE로 판단하지 않는다.
- 계속 작업해야 하면 응답에 CONTINUE를 포함한다.
- 모든 acceptance criteria를 실제로 통과하면 응답에 정확히 COMPLETE를 포함한다.

현재 GitHub 프로젝트 상태와 result/error JSON을 먼저 확인하고 sequence ${nextSequence}의 미완료 지점부터 진행해.
검증된 작업은 반복하지 말고 실제 오류가 있으면 직접 수정한 뒤 검증해.`;

    // Advance sequence before sending so a response can never dispatch the same task twice.
    await setState({ sequence:nextSequence, status:"continue", lastError:"", lastResponse:"", lastResponseAt:0 });
    await sendPrompt(prompt);
  } catch (error) {
    await stopRun("blocked", error?.message || String(error));
  } finally {
    dispatching = false;
  }
}

async function startRun() {
  if (dispatching) return;
  clearRunTimers();
  const targetTabId = await resolveTargetTab();
  const state = await setState({ running:true, sequence:0, taskId:"corridor-employee-movement", status:"continue", lastError:"", lastResponse:"", lastResponseAt:0, startedAt:Date.now(), targetTabId });

  checkpointTimer = setTimeout(async () => {
    const current = await getState();
    if (!current.running || current.status !== "continue") return;
    await setState({ status:"continue", lastError:"18분 checkpoint: 자동 작업을 계속합니다." });
    await dispatchNext("CHECKPOINT");
  }, CHECKPOINT_MS);
  hardStopTimer = setTimeout(() => stopRun("blocked", "20분 hard stop에 도달했습니다."), HARD_STOP_MS);
  await dispatchNext("START");
  return state;
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "get_state") { getState().then(state => sendResponse({ok:true,state})); return true; }
  if (message?.type === "set_state") { setState(message.patch || {}).then(state => sendResponse({ok:true,state})); return true; }
  if (message?.type === "start_run") { startRun().then(state => sendResponse({ok:true,state})).catch(error => sendResponse({ok:false,error:error?.message||String(error)})); return true; }
  if (message?.type === "continue_run") {
    getState().then(async state => {
      if (!state.taskId) throw new Error("재개할 task가 없습니다.");
      clearRunTimers();
      await setState({running:true,status:"continue",lastError:""});
      hardStopTimer = setTimeout(() => stopRun("blocked", "20분 hard stop에 도달했습니다."), HARD_STOP_MS);
      await dispatchNext("CONTINUE");
      sendResponse({ok:true});
    }).catch(error => sendResponse({ok:false,error:error?.message||String(error)}));
    return true;
  }
  if (message?.type === "stop_run") { stopRun("stopped", "").then(() => sendResponse({ok:true})); return true; }
  if (message?.type === "send_prompt") {
    sendPrompt(String(message.prompt || "")).then(async result => { await setState({status:"continue",lastError:""}); sendResponse(result); }).catch(async error => { const text=error?.message||String(error); await setState({status:"blocked",lastError:text,running:false}); sendResponse({ok:false,error:text}); });
    return true;
  }
  if (message?.type === "assistant_response") {
    const text=String(message.text||"");
    const timestamp=Number(message.timestamp||Date.now());
    setState({lastResponse:text,lastResponseAt:timestamp}).then(async () => {
      const state=await getState();
      if (!state.running || state.status!=="continue") return;
      if (timestamp <= state.lastResponseAt && !text) return;
      if (/\bCOMPLETE\b/i.test(text)) { await stopRun("complete", ""); return; }
      if (/\bCONTINUE\b/i.test(text)) await dispatchNext("CONTINUE");
    }).catch(()=>{});
    return;
  }
});
