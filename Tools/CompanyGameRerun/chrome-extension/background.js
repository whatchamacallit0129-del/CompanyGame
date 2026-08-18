const CHATGPT_URL = "https://chatgpt.com/";
const CHATGPT_HOSTS = ["chatgpt.com", "www.chatgpt.com"];
const STORAGE_KEY = "companygame_rerun_state";
const DEFAULT_STATE = { running: false, sequence: 0, taskId: "", status: "stopped", lastError: "", lastResponse: "", lastResponseAt: 0, startedAt: 0 };

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

async function findOrCreateChatGPTTab() {
  const tabs = await chrome.tabs.query({});
  let tab = tabs.find(isChatGPTTab);
  if (!tab) {
    tab = await chrome.tabs.create({ url: CHATGPT_URL, active: true });
    await new Promise(resolve => setTimeout(resolve, 3000));
  } else if (tab.id) {
    await chrome.tabs.update(tab.id, { active: true });
    await new Promise(resolve => setTimeout(resolve, 500));
  }
  if (!tab?.id) throw new Error("ChatGPT 탭을 찾거나 만들지 못했습니다.");
  return tab.id;
}

async function ensureContentScript(tabId) {
  try {
    const response = await chrome.tabs.sendMessage(tabId, { type: "ping" });
    if (response?.ok) return;
  } catch (_) {}

  await chrome.scripting.executeScript({ target: { tabId }, files: ["content.js"] });
  await new Promise(resolve => setTimeout(resolve, 500));
  const response = await chrome.tabs.sendMessage(tabId, { type: "ping" });
  if (!response?.ok) throw new Error("ChatGPT content script 연결에 실패했습니다.");
}

async function sendPrompt(prompt) {
  const tabId = await findOrCreateChatGPTTab();
  await ensureContentScript(tabId);
  const result = await chrome.tabs.sendMessage(tabId, { type: "send_prompt", prompt });
  if (!result?.ok) throw new Error(result?.error || "ChatGPT 메시지 전송에 실패했습니다.");
  return { ok: true, tabId };
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "get_state") {
    getState().then(state => sendResponse({ ok: true, state }));
    return true;
  }

  if (message?.type === "set_state") {
    setState(message.patch || {}).then(state => sendResponse({ ok: true, state }));
    return true;
  }

  if (message?.type === "send_prompt") {
    sendPrompt(String(message.prompt || ""))
      .then(async result => {
        await setState({ status: "continue", lastError: "" });
        sendResponse(result);
      })
      .catch(async error => {
        const text = error?.message || String(error);
        await setState({ status: "blocked", lastError: text, running: false });
        sendResponse({ ok: false, error: text });
      });
    return true;
  }

  if (message?.type === "assistant_response") {
    const text = String(message.text || "");
    const timestamp = Number(message.timestamp || Date.now());
    setState({ lastResponse: text, lastResponseAt: timestamp }).then(() => {
      // Forward the observed assistant response to the Side Panel.
      chrome.runtime.sendMessage({ type: "assistant_response", text, timestamp }).catch(() => {});
    }).catch(() => {});
    return;
  }

  if (message?.type === "test_connection") {
    sendPrompt("RERUN_TEST_OK — CompanyGame Rerun 연결 테스트입니다. 이 메시지가 ChatGPT 대화에 보이면 Chrome → ChatGPT 통신이 정상입니다.")
      .then(sendResponse)
      .catch(error => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }
});
