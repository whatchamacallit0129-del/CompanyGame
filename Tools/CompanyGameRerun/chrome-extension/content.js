function findComposer() {
  const candidates = [
    document.querySelector('#prompt-textarea'),
    document.querySelector('div[contenteditable="true"][role="textbox"]'),
    document.querySelector('div[contenteditable="true"][data-placeholder]'),
    document.querySelector('textarea[placeholder*="Message"]'),
    document.querySelector('textarea[data-testid*="textbox"]'),
    document.querySelector('textarea')
  ].filter(Boolean);
  return candidates.find(el => !el.disabled && el.getAttribute('aria-disabled') !== 'true') || null;
}

function findSendButton() {
  const selectors = [
    'button[data-testid="send-button"]',
    'button[aria-label="Send prompt"]',
    'button[aria-label="Send message"]',
    'button[aria-label*="Send"]',
    'button[aria-label*="보내기"]'
  ];
  return selectors.map(s => document.querySelector(s)).find(Boolean) || null;
}

function composerText(el) {
  if (!el) return '';
  if (el instanceof HTMLTextAreaElement) return el.value || '';
  return (el.innerText || el.textContent || '').trim();
}

function setTextareaValue(el, text) {
  const setter = Object.getOwnPropertyDescriptor(HTMLTextAreaElement.prototype, 'value')?.set;
  if (!setter) throw new Error('INPUT_SETTER_FAILED: textarea value setter가 없습니다.');
  setter.call(el, text);
  el.dispatchEvent(new InputEvent('input', { bubbles:true, inputType:'insertText', data:text }));
  el.dispatchEvent(new Event('change', { bubbles:true }));
}

function setContentEditableValue(el, text) {
  el.focus();
  const selection = window.getSelection();
  const range = document.createRange();
  range.selectNodeContents(el);
  selection.removeAllRanges();
  selection.addRange(range);

  let ok = false;
  try { ok = document.execCommand('insertText', false, text); } catch (_) {}

  if (!ok || !composerText(el)) {
    el.replaceChildren();
    const p = document.createElement('p');
    p.textContent = text;
    el.appendChild(p);
  }

  el.dispatchEvent(new InputEvent('beforeinput', {
    bubbles:true,
    cancelable:true,
    inputType:'insertText',
    data:text
  }));
  el.dispatchEvent(new InputEvent('input', {
    bubbles:true,
    cancelable:true,
    inputType:'insertText',
    data:text
  }));
  el.dispatchEvent(new Event('change', { bubbles:true }));
}

function setComposerValue(el, text) {
  if (el instanceof HTMLTextAreaElement) setTextareaValue(el, text);
  else setContentEditableValue(el, text);
}

async function waitForComposer(timeout = 10000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    const composer = findComposer();
    if (composer) return composer;
    await new Promise(r => setTimeout(r, 200));
  }
  throw new Error('COMPOSER_NOT_FOUND: ChatGPT 입력창을 찾지 못했습니다.');
}

async function waitForSendButton(timeout = 4000) {
  const started = Date.now();
  while (Date.now() - started < timeout) {
    const button = findSendButton();
    if (button && !button.disabled && button.getAttribute('aria-disabled') !== 'true') return button;
    await new Promise(r => setTimeout(r, 100));
  }
  return null;
}

function submitByEnter(el) {
  el.focus();
  const options = { key:'Enter', code:'Enter', keyCode:13, which:13, bubbles:true, cancelable:true };
  el.dispatchEvent(new KeyboardEvent('keydown', options));
  el.dispatchEvent(new KeyboardEvent('keypress', options));
  el.dispatchEvent(new KeyboardEvent('keyup', options));
}

async function submitComposer(el) {
  const button = await waitForSendButton();
  if (button) {
    button.click();
    return 'button';
  }
  submitByEnter(el);
  await new Promise(r => setTimeout(r, 500));
  return 'enter';
}

function getAssistantMessages() {
  return [...document.querySelectorAll('[data-message-author-role="assistant"]')]
    .map(n => (n.innerText || n.textContent || '').trim())
    .filter(Boolean);
}

let observerInstalled = false;
let lastReportedAssistant = '';
let lastPromptAt = 0;
let responseDebounce = null;

function reportAssistantResponse(text) {
  text = String(text || '').trim();
  if (!text || text === lastReportedAssistant) return;
  lastReportedAssistant = text;
  chrome.runtime.sendMessage({ type:'assistant_response', text, timestamp:Date.now() }).catch(() => {});
}

function installAssistantObserver() {
  if (observerInstalled) return;
  observerInstalled = true;

  const inspect = () => {
    if (!lastPromptAt) return;
    const messages = getAssistantMessages();
    if (!messages.length) return;
    const latest = messages[messages.length - 1];
    if (!latest) return;

    // Ignore the assistant message that existed before our automated prompt.
    if (messages.length === 1 && latest === lastReportedAssistant) return;

    clearTimeout(responseDebounce);
    responseDebounce = setTimeout(() => {
      const stable = getAssistantMessages().at(-1) || '';
      if (stable) reportAssistantResponse(stable);
    }, 1200);
  };

  new MutationObserver(inspect).observe(document.documentElement, {
    childList:true,
    subtree:true,
    characterData:true
  });
  setInterval(inspect, 1000);
}

async function sendPrompt(prompt) {
  prompt = String(prompt || '').trim();
  if (!prompt) throw new Error('EMPTY_PROMPT: 빈 프롬프트입니다.');

  const composer = await waitForComposer();
  lastPromptAt = Date.now();
  lastReportedAssistant = getAssistantMessages().at(-1) || lastReportedAssistant;

  setComposerValue(composer, prompt);
  await new Promise(r => setTimeout(r, 500));

  if (!composerText(composer)) {
    throw new Error('INPUT_NOT_REFLECTED: 입력창에 프롬프트가 반영되지 않았습니다.');
  }

  const method = await submitComposer(composer);
  await new Promise(r => setTimeout(r, 700));

  // Return immediately after successful submission. Response monitoring is independent
  // so the background worker never blocks waiting for a content-script promise.
  return { method, composer:'ok' };
}

chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (message?.type === 'ping') {
    sendResponse({ ok:true, ready:true, composerFound:!!findComposer(), sendButtonFound:!!findSendButton() });
    return;
  }

  if (message?.type === 'send_prompt') {
    sendPrompt(message.prompt)
      .then(info => sendResponse({ ok:true, ...info }))
      .catch(error => sendResponse({ ok:false, error:error?.message || String(error) }));
    return true;
  }
});

installAssistantObserver();
