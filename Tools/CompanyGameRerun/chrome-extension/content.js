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
  const selectors = [
    'button[data-testid="send-button"]',
    'button[aria-label="Send prompt"]',
    'button[aria-label="Send message"]',
    'button[aria-label*="Send"]',
    'button[aria-label*="보내기"]'
  ];
  return selectors.map(selector => document.querySelector(selector)).find(Boolean);
}

function setTextareaValue(textarea, text) {
  const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, "value")?.set;
  if (!setter) throw new Error("ChatGPT textarea value setter를 찾지 못했습니다.");
  setter.call(textarea, text);
  textarea.dispatchEvent(new InputEvent("input", {
    bubbles: true,
    cancelable: true,
    inputType: "insertText",
    data: text
  }));
  textarea.dispatchEvent(new Event("change", { bubbles: true }));
}

function setContentEditableValue(element, text) {
  element.focus();

  // React/ProseMirror 계열 입력창에서는 DOM만 바꾸면 내부 상태가 갱신되지
  // 않을 수 있다. execCommand('insertText')를 먼저 사용하고, 실패할 경우
  // Selection + InputEvent 방식으로 fallback한다.
  const selection = window.getSelection();
  const range = document.createRange();
  range.selectNodeContents(element);
  selection.removeAllRanges();
  selection.addRange(range);

  let inserted = false;
  try {
    inserted = document.execCommand("insertText", false, text);
  } catch (_) {}

  if (!inserted) {
    element.replaceChildren();
    const paragraph = document.createElement("p");
    paragraph.textContent = text;
    element.appendChild(paragraph);
    element.dispatchEvent(new InputEvent("input", {
      bubbles: true,
      cancelable: true,
      inputType: "insertText",
      data: text
    }));
  }

  // 일부 버전의 ChatGPT는 input 이후에도 React 상태 갱신을 위해
  // beforeinput/input 이벤트를 요구한다.
  element.dispatchEvent(new Event("change", { bubbles: true }));
}

function setComposerValue(composer, text) {
  composer.focus();
  if (composer instanceof HTMLTextAreaElement) {
    setTextareaValue(composer, text);
  } else {
    setContentEditableValue(composer, text);
  }
}

async function waitForComposer(timeout = 15000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    const composer = findComposer();
    if (composer && !composer.disabled && composer.getAttribute("aria-disabled") !== "true") return composer;
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error("ChatGPT 입력창을 찾지 못했습니다. 현재 이 대화 화면이 열려 있는지 확인하세요.");
}

async function waitForSendButton(timeout = 5000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    const button = findSendButton();
    if (button && !button.disabled && button.getAttribute("aria-disabled") !== "true") return button;
    await new Promise(resolve => setTimeout(resolve, 150));
  }
  return null;
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
  responseObserver.observe(document.documentElement, {
    childList: true,
    subtree: true,
    characterData: true
  });
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

async function submitComposer(composer) {
  // 버튼 클릭이 가장 안정적이다.
  const send = await waitForSendButton();
  if (send) {
    send.click();
    return;
  }

  // 버튼이 아직 렌더링되지 않은 경우 실제 KeyboardEvent를 입력창에 전달한다.
  composer.focus();
  composer.dispatchEvent(new KeyboardEvent("keydown", {
    key: "Enter",
    code: "Enter",
    keyCode: 13,
    which: 13,
    bubbles: true,
    cancelable: true
  }));
}

async function sendPrompt(prompt) {
  if (!prompt.trim()) throw new Error("빈 프롬프트는 전송할 수 없습니다.");

  startAssistantObserver();
  const baseline = getLatestAssistantText();
  const composer = await waitForComposer();

  setComposerValue(composer, prompt);

  // ChatGPT React 상태가 실제 입력을 인식할 시간을 준다.
  await new Promise(resolve => setTimeout(resolve, 700));

  const visibleValue = composer instanceof HTMLTextAreaElement
    ? composer.value
    : composer.innerText || composer.textContent || "";

  if (!visibleValue.trim()) {
    throw new Error("ChatGPT 입력창에 프롬프트가 반영되지 않았습니다.");
  }

  await submitComposer(composer);

  const responseText = await waitForNewAssistantResponse(baseline);
  chrome.runtime.sendMessage({
    type: "assistant_response",
    text: responseText,
    timestamp: Date.now()
  }).catch(() => {});
  return responseText;
}

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

startAssistantObserver();
