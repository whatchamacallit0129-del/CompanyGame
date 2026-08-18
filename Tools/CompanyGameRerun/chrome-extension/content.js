function findComposer() {
  return document.querySelector('textarea[placeholder*="Message"], textarea[data-testid*="textbox"], div[contenteditable="true"]');
}

function findSendButton() {
  return document.querySelector('button[data-testid="send-button"], button[aria-label*="Send"]');
}

async function sendPrompt(prompt) {
  const composer = findComposer();
  if (!composer) throw new Error("ChatGPT composer not found");

  composer.focus();
  if (composer.tagName === "TEXTAREA") {
    const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, "value")?.set;
    setter?.call(composer, prompt);
    composer.dispatchEvent(new Event("input", { bubbles: true }));
  } else {
    composer.textContent = prompt;
    composer.dispatchEvent(new InputEvent("input", { bubbles: true, inputType: "insertText", data: prompt }));
  }

  await new Promise(r => setTimeout(r, 150));
  const send = findSendButton();
  if (!send) throw new Error("ChatGPT send button not found");
  send.click();
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === "send_prompt") {
    sendPrompt(message.prompt)
      .then(() => sendResponse({ ok: true }))
      .catch(error => sendResponse({ ok: false, error: String(error.message || error) }));
    return true;
  }
});
