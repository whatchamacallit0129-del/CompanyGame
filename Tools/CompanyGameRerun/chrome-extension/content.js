function findComposer() {
  const selectors = [
    '#prompt-textarea',
    'div[contenteditable="true"][data-placeholder]',
    'div[contenteditable="true"][role="textbox"]',
    'textarea[placeholder*="Message"]',
    'textarea[data-testid*="textbox"]',
    'textarea'
  ];
  return selectors.map(selector => document.querySelector(selector)).find(Boolean);
}

function findSendButton() {
  return document.querySelector('button[data-testid="send-button"], button[aria-label="Send prompt"], button[aria-label*="Send"], button[aria-label*="보내기"]');
}

function setComposerValue(composer, text) {
  composer.focus();

  if (composer instanceof HTMLTextAreaElement) {
    const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, "value")?.set;
    if (!setter) throw new Error("textarea value setter를 찾지 못했습니다.");
    setter.call(composer, text);
    composer.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: text }));
    composer.dispatchEvent(new Event("change", { bubbles: true }));
    return;
  }

  // ChatGPT currently uses a contenteditable prompt in many builds.
  composer.replaceChildren();
  const paragraph = document.createElement("p");
  paragraph.textContent = text;
  composer.appendChild(paragraph);
  composer.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: text }));
}

async function waitForComposer(timeout = 15000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    const composer = findComposer();
    if (composer) return composer;
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error("ChatGPT composer를 찾지 못했습니다. ChatGPT 대화 화면이 완전히 로드되었는지 확인하세요.");
}

async function sendPrompt(prompt) {
  if (!prompt.trim()) throw new Error("빈 프롬프트는 전송할 수 없습니다.");

  const composer = await waitForComposer();
  setComposerValue(composer, prompt);
  await new Promise(resolve => setTimeout(resolve, 500));

  const send = findSendButton();
  if (send && !send.disabled) {
    send.click();
    return;
  }

  // Fallback: submit through Enter if the send button is not exposed.
  composer.dispatchEvent(new KeyboardEvent("keydown", {
    key: "Enter", code: "Enter", bubbles: true, cancelable: true
  }));
  composer.dispatchEvent(new KeyboardEvent("keyup", {
    key: "Enter", code: "Enter", bubbles: true
  }));
}

function extractAssistantText(node) {
  const role = node?.getAttribute?.("data-message-author-role");
  if (role !== "assistant") return "";
  return node.innerText?.trim() || "";
}

let lastObservedAssistantText = "";
let observerStarted = false;

function startAssistantObserver() {
  if (observerStarted) return;
  observerStarted = true;

  const scan = () => {
    const nodes = document.querySelectorAll('[data-message-author-role="assistant"]');
    if (!nodes.length) return;
    const text = extractAssistantText(nodes[nodes.length - 1]);
    if (!text || text === lastObservedAssistantText) return;
    lastObservedAssistantText = text;
    chrome.runtime.sendMessage({ type: "assistant_response", text, timestamp: Date.now() }).catch(() => {});
  };

  const observer = new MutationObserver(scan);
  observer.observe(document.documentElement, { childList: true, subtree: true, characterData: true });
  scan();
}

startAssistantObserver();

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "ping") {
    sendResponse({ ok: true, ready: true });
    return;
  }

  if (message?.type === "send_prompt") {
    sendPrompt(String(message.prompt || ""))
      .then(() => sendResponse({ ok: true }))
      .catch(error => sendResponse({ ok: false, error: error?.message || String(error) }));
    return true;
  }
});
