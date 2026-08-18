const STORAGE_KEY = "companygame_rerun_state";
const state = { running:false, sequence:0, taskId:"corridor-employee-movement", status:"stopped", lastError:"", lastResponse:"", lastResponseAt:0, startedAt:0 };
const $ = id => document.getElementById(id);
let processing = false;

async function syncState(){
  const result = await chrome.runtime.sendMessage({type:"get_state"});
  if(result?.ok) Object.assign(state,result.state);
  render();
}
async function save(){ await chrome.runtime.sendMessage({type:"set_state",patch:{...state}}); }
function render(){ $("status").textContent = state.status === "complete" ? "Rerun 완료" : state.status === "blocked" ? "Rerun 대기/차단" : state.running ? "Rerun 실행 중" : "Rerun 정지"; $("details").textContent=JSON.stringify(state,null,2); }

async function send(prompt){
  const r=await chrome.runtime.sendMessage({type:"send_prompt",prompt});
  if(!r?.ok) throw new Error(r?.error||"ChatGPT 메시지 전송 실패");
  state.status="continue"; state.lastError=""; await save(); render();
}

function buildPrompt(kind){
 return `${kind} — CompanyGame Rerun 작업 authorization.
현재 작업 범위는 게임적인 부분만이다.
Task ID: ${state.taskId}
Sequence: ${state.sequence}
목표: 통로(Node)와 직원 이동 시스템을 로보토미 코퍼레이션과 유사한 조작감으로 완성한다.

요구사항:
- 기존에 검증된 기능은 반복하지 않는다.
- 통로와 Node는 확장 가능한 소프트코딩 구조로 유지한다.
- 직원 선택 → 목적지 지정 → Node 경로 탐색 → Corridor를 따라 실제 이동 → 목적지 도착까지 구현한다.
- 이동속도, 가속/감속, 도착 거리, 그룹 간격 등은 설정 데이터로 분리한다.
- result.json과 error.json을 직접 확인하고 오류가 있으면 원인을 분석하여 수정한다.
- Unity에서 실제 직원이 연결된 Node를 순서대로 따라 목적지까지 이동하는 것을 검증하기 전에는 COMPLETE로 판단하지 않는다.
- 계속 작업해야 하면 응답에 CONTINUE를 포함한다.
- 모든 acceptance criteria를 실제로 통과하면 응답에 정확히 COMPLETE를 포함한다.

현재 GitHub 프로젝트 상태와 result/error JSON을 먼저 확인하고 현재 sequence의 미완료 지점부터 진행해.`;
}

async function start(kind){
 if(processing) return; processing=true;
 try{
   if(kind==="START"){
     state.running=true; state.sequence+=1; state.taskId="corridor-employee-movement"; state.status="continue"; state.lastResponse=""; state.lastResponseAt=0; state.startedAt=Date.now();
   }else{
     state.running=true; state.status="continue"; state.lastError="";
   }
   await save(); render(); await send(buildPrompt(kind));
 }catch(e){state.running=false;state.status="blocked";state.lastError=e?.message||String(e);await save();render();}
 finally{processing=false;}
}

$("start").onclick=()=>start("START");
$("resume").onclick=()=>start("CONTINUE");
$("stop").onclick=async()=>{state.running=false;state.status="stopped";await save();render();};

chrome.runtime.onMessage.addListener(async message=>{
 if(message?.type!=="assistant_response") return;
 state.lastResponse=String(message.text||""); state.lastResponseAt=Number(message.timestamp||Date.now()); await save(); render();
 if(!state.running || state.status!=="continue" || processing) return;
 if(state.lastResponseAt<=state.startedAt) return;
 const response=state.lastResponse;
 if(/\bCOMPLETE\b/i.test(response)){state.running=false;state.status="complete";await save();render();return;}
 if(/\bCONTINUE\b/i.test(response)){
   processing=true;
   try{
     state.sequence+=1; state.lastResponse=""; state.lastResponseAt=0; await save(); render();
     await send(`CONTINUE — 같은 task(${state.taskId})의 sequence ${state.sequence}을 진행한다.\n방금 응답 이후의 실제 미완료 지점부터 작업해.\nGitHub 현재 상태와 result.json/error.json을 확인하고 검증된 작업은 반복하지 마.\n실제 직원이 Node Graph를 따라 목적지까지 이동하는 것이 완료 조건이다.\n오류가 있으면 직접 분석하고 수정한 뒤 다시 검증해.\n완료되면 COMPLETE, 추가 작업이 필요하면 CONTINUE라고 명확하게 보고해.`);
   }catch(e){state.running=false;state.status="blocked";state.lastError=e?.message||String(e);await save();render();}
   finally{processing=false;}
 }
});

syncState().catch(e=>{state.status="blocked";state.lastError=e?.message||String(e);render();});
