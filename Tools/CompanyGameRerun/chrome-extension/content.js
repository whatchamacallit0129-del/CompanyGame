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
  throw new Error("ChatGPT composer를 찾지 못했습니다. 현재 ChatGPT 대화 화면이 열려 있는지 확인하세요.");
}

function getLatestAssistantText() {
  const nodes = document.querySelectorAll('[data-message-author-role="assistant"]');
  if (!nodes.length) return "";
  return nodes[nodes.length - 1].innerText?.trim() || "";
}

let responseWaiter = null;
let responseObserver = null;
let observerStarted = false;
let responseGeneration = 0;

function startAssistantObserver() {
  if (observerStarted) return;
  observerStarted = true;
  responseObserver = new MutationObserver(() => {
    if (!responseWaiter) return;
    const text = getLatestAssistantText();
    if (!text || text === responseWaiter.baseline) return;
    responseWaiter.latest = text;
    responseWaiter.lastChangedAt = Date.now();
  });
  responseObserver.observe(document.documentElement, { childList: true, subtree: true, characterData: true });
}

function waitForNewAssistantResponse(baseline, timeout = 120000) {
  startAssistantObserver();
  responseGeneration += 1;
  const generation = responseGeneration;

  return new Promise((resolve, reject) => {
    const started = Date.now();
    responseWaiter = { baseline, latest: "", lastChangedAt: 0, generation };

    const poll = () => {
      if (!responseWaiter || responseWaiter.generation !== generation) return;
      const latest = getLatestAssistantText();
      if (latest && latest !== baseline) {
        responseWaiter.latest = latest;
        if (!responseWaiter.lastChangedAt) responseWaiter.lastChangedAt = Date.now();
        // Wait briefly for streaming text to settle before handing it to the engine.
        if (Date.now() - responseWaiter.lastChangedAt >= 900) {
          const result = responseWaiter.latest;
          responseWaiter = null;
          resolve(result);
          return;
        }
      }
      if (Date.now() - started >= timeout) {
        responseWaiter = null;
        reject(new Error("새 ChatGPT 응답을 기다리는 시간이 초과되었습니다."));
        return;
      }
      setTimeout(poll, 300);
    };
    poll();
  });
}

async function sendPrompt(prompt) {
  if (!prompt.trim()) throw new Error("빈 프롬프트는 전송할 수 없습니다.");

  startAssistantObserver();
  const baseline = getLatestAssistantText();
  const composer = await waitForComposer();
  setComposerValue(composer, prompt);
  await new Promise(resolve => setTimeout(resolve, 500));

  const send = findSendButton();
  if (send && !send.disabled) {
    send.click();
  } else {
    composer.dispatchEvent(new KeyboardEvent("keydown", { key:"Enter", code:"Enter", bubbles:true, cancelable:true }));
    composer.dispatchEvent(new KeyboardEvent("keyup", { key:"Enter", code:"Enter", bubbles:true }));
  }

  const responseText = await waitForNewAssistantResponse(baseline);
  chrome.runtime.sendMessage({ type:"assistant_response", text:responseText, timestamp:Date.now() }).catch(() => {});
  return responseText;
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "ping") {
    sendResponse({ ok:true, ready:true });
    return;
  }

  if (message?.type === "send_prompt") {
    sendPrompt(String(message.prompt || ""))
      .then(() => sendResponse({ ok:true }))
      .catch(error => sendResponse({ ok:false, error:error?.message || String(error) }));
    return true;
  }
});

startAssistantObserver();
