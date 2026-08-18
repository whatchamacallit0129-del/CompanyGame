function findComposer() {
  const selectors = [
    'textarea[placeholder*="Message"]',
    'textarea[data-testid*="textbox"]',
    'textarea',
    'div[contenteditable="true"][role="textbox"]',
    'div[contenteditable="true"]'
  ];
  return selectors.map(selector => document.querySelector(selector)).find(Boolean);
}

function findSendButton() {
  return document.querySelector('button[data-testid="send-button"], button[aria-label*="Send"], button[aria-label*="보내기"]');
}

function setComposerValue(composer, text) {
  composer.focus();
  if (composer instanceof HTMLTextAreaElement) {
    const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, "value")?.set;
    setter?.call(composer, text);
    composer.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: text }));
    composer.dispatchEvent(new Event("change", { bubbles: true }));
    return;
  }

  composer.textContent = text;
  composer.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: text }));
}

async function waitForComposer(timeout = 10000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    const composer = findComposer();
    if (composer) return composer;
    await new Promise(resolve => setTimeout(resolve, 250));
  }
  throw new Error("ChatGPT composer not found after waiting.");
}

async function sendPrompt(prompt) {
  const composer = await waitForComposer();
  setComposerValue(composer, prompt);
  await new Promise(resolve => setTimeout(resolve, 300));

  const send = findSendButton();
  if (send && !send.disabled) {
    send.click();
    return;
  }

  composer.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter", code: "Enter", bubbles: true }));
  composer.dispatchEvent(new KeyboardEvent("keyup", { key: "Enter", code: "Enter", bubbles: true }));
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
