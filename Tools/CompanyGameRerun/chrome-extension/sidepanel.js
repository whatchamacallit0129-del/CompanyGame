const state = { running:false, sequence:0, taskId:"", status:"stopped", lastError:"", lastResponse:"", lastResponseAt:0, startedAt:0 };
const $ = id => document.getElementById(id);
let refreshing = false;

async function syncState(){
  if(refreshing) return;
  refreshing = true;
  try {
    const result = await chrome.runtime.sendMessage({type:"get_state"});
    if(result?.ok) Object.assign(state,result.state);
    render();
  } finally { refreshing = false; }
}
function render(){
  $("status").textContent = state.status === "complete" ? "Rerun 완료" : state.status === "blocked" ? "Rerun 대기/차단" : state.running ? "Rerun 실행 중" : "Rerun 정지";
  $("details").textContent = JSON.stringify(state,null,2);
}
async function call(type){
  const result = await chrome.runtime.sendMessage({type});
  if(!result?.ok) throw new Error(result?.error || `${type} 실패`);
  await syncState();
}

$("start").onclick = async () => {
  try { await call("start_run"); }
  catch(e){ state.status="blocked"; state.lastError=e?.message||String(e); render(); }
};

$("resume").onclick = async () => {
  try { await call("continue_run"); }
  catch(e){ state.status="blocked"; state.lastError=e?.message||String(e); render(); }
};

$("stop").onclick = async () => {
  try { await call("stop_run"); }
  catch(e){ state.status="blocked"; state.lastError=e?.message||String(e); render(); }
};

chrome.runtime.onMessage.addListener(message => {
  if(message?.type === "assistant_response") syncState();
});

setInterval(syncState, 1000);
syncState().catch(e => { state.status="blocked"; state.lastError=e?.message||String(e); render(); });
