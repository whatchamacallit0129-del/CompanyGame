const CHATGPT_URL = "https://chatgpt.com/";
const CHATGPT_HOSTS = ["chatgpt.com", "www.chatgpt.com"];

chrome.runtime.onInstalled.addListener(async () => {
  await chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {});
});

function isChatGPTTab(tab) {
  return !!tab?.url && CHATGPT_HOSTS.some(host => tab.url.startsWith(`https://${host}/`));
}

async function findOrCreateChatGPTTab() {
  const tabs = await chrome.tabs.query({});
  let tab = tabs.find(isChatGPTTab);
  if (!tab) {
    tab = await chrome.tabs.create({ url: CHATGPT_URL, active: true });
    await new Promise(resolve => setTimeout(resolve, 2500));
  } else if (tab.id) {
    await chrome.tabs.update(tab.id, { active: true });
    await new Promise(resolve => setTimeout(resolve, 300));
  }
  if (!tab?.id) throw new Error("ChatGPT 탭을 만들거나 찾지 못했습니다.");
  return tab.id;
}

async function ensureContentScript(tabId) {
  try {
    await chrome.tabs.sendMessage(tabId, { type: "ping" });
  } catch (_) {
    await chrome.scripting.executeScript({ target: { tabId }, files: ["content.js"] });
    await new Promise(resolve => setTimeout(resolve, 300));
  }
}

async function sendPrompt(prompt) {
  const tabId = await findOrCreateChatGPTTab();
  await ensureContentScript(tabId);
  const result = await chrome.tabs.sendMessage(tabId, { type: "send_prompt", prompt });
  if (!result?.ok) throw new Error(result?.error || "ChatGPT 메시지 전송에 실패했습니다.");
  return { ok: true, tabId };
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "send_prompt") {
    sendPrompt(String(message.prompt || ""))
      .then(sendResponse)
      .catch(error => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }
  if (message?.type === "test_connection") {
    sendPrompt("RERUN_TEST_OK — CompanyGame Rerun 연결 테스트입니다.")
      .then(sendResponse)
      .catch(error => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }
});
