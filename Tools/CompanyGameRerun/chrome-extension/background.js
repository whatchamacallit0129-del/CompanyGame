const CHATGPT_HOSTS = ["chatgpt.com", "www.chatgpt.com"];

chrome.runtime.onInstalled.addListener(() => {
  chrome.sidePanel.setPanelBehavior({ openPanelOnActionClick: true }).catch(() => {});
});

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "open_chatgpt") {
    chrome.tabs.query({}, (tabs) => {
      const tab = tabs.find(t => t.url && CHATGPT_HOSTS.some(h => t.url.includes(h)));
      if (tab?.id) {
        chrome.tabs.update(tab.id, { active: true });
        sendResponse({ ok: true, tabId: tab.id });
      } else {
        chrome.tabs.create({ url: "https://chatgpt.com/" }, created => sendResponse({ ok: true, tabId: created.id }));
      }
    });
    return true;
  }
});
