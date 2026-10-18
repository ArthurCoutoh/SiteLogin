"use strict";
(()=>{
 const api=window.PortfolioAuth,panel=document.getElementById('activity-panel'),users=document.getElementById('activity-users'),detail=document.getElementById('activity-detail'),status=document.getElementById('activity-status'),search=document.getElementById('activity-search');
 let selected=null,eventCursor=null,sessionCursor=null,busy=false,generation=0,historyExpanded=false;
 const el=(tag,text,cls)=>{const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n;};
 const date=value=>value?new Date(value).toLocaleString('pt-BR',{timeZone:'America/Manaus',dateStyle:'short',timeStyle:'medium'}):'—';
 const duration=value=>{const s=Math.max(0,Number(value)||0);return Math.floor(s/3600)+'h '+Math.floor(s%3600/60)+'min '+Math.floor(s%60)+'s';};
 async function loadUsers(){
  const data=await api.request('/api/admin/activity/users?q='+encodeURIComponent(search.elements.q.value));users.replaceChildren();status.textContent=data.users.length?data.users.length+' usuário(s) · atualizado em '+date(data.serverTime):'Nenhum usuário encontrado.';
  for(const row of data.users){
   const card=el('article',undefined,'student-card activity-user');card.append(el('h3',row.name),el('p',row.email,'auth-note'));
   card.append(el('span',row.online?'Online':'Offline','presence-label '+(row.online?'is-online':'')),el('p','Último sinal: '+date(row.lastSeenAt),'auth-note'),el('p','Tempo de sessões: '+duration(row.onlineSeconds),'auth-note'));
   const button=el('button','Ver atividade','friend-action');button.type='button';button.addEventListener('click',()=>{selected=row.id;historyExpanded=false;users.hidden=true;detail.hidden=false;refreshDetail().catch(error=>status.textContent=error.message);});card.append(button);users.append(card);
  }
 }
 function sessions(rows,append=false){
  const body=document.getElementById('activity-sessions');if(!append)body.replaceChildren();
  if(!rows.length&&!append){const tr=el('tr'),td=el('td','Sem sessões registradas.');td.colSpan=5;tr.append(td);body.append(tr);}
  for(const row of rows){const tr=el('tr');const end=row.exitReason==='logout'?'Logout: '+date(row.logoutAt):row.exitReason==='expired'?'Sessão expirada':row.exitReason==='revoked'?'Sessão revogada':row.online?'Em andamento':'Sem sinal desde '+date(row.lastSeenAt);
   for(const text of [row.loginAt?date(row.loginAt):'Primeiro registro: '+date(row.observedAt),date(row.lastSeenAt),end,duration(row.onlineSeconds),row.online?'Online':row.logoutAt?'Encerrada':'Inativa'])tr.append(el('td',text));body.append(tr);}
 }
 function events(rows,append=false){
  const box=document.getElementById('activity-events');if(!append)box.replaceChildren();if(!rows.length&&!append)box.append(el('p','Sem eventos registrados.','auth-note'));
  for(const row of rows){const item=el('div',undefined,'activity-event');item.append(el('time',date(row.occurredAt)),el('span',row.type==='login'?'Login':row.type==='logout'?'Logout':'Acessou '+row.path));box.append(item);}
 }
 async function refreshDetail(kind){
  if(kind)historyExpanded=true;const id=selected,version=++generation;let url='/api/admin/activity/users/'+id;if(kind==='events'&&eventCursor)url+='?before='+eventCursor;if(kind==='sessions'&&sessionCursor)url+='?beforeSession='+sessionCursor;
  const data=await api.request(url);if(id!==selected||version!==generation)return;status.textContent='Atualizado em '+date(data.serverTime);document.getElementById('activity-name').textContent=data.name+' · '+data.email;
  if(kind!=='events'){sessions(data.sessions,kind==='sessions');sessionCursor=data.nextBeforeSession;document.getElementById('activity-more-sessions').hidden=!sessionCursor;}
  if(kind!=='sessions'){events(data.events,kind==='events');eventCursor=data.nextBefore;document.getElementById('activity-more-events').hidden=!eventCursor;}
 }
 async function refresh(){if(panel.hidden||document.hidden||busy)return;busy=true;try{if(selected!==null){if(!historyExpanded)await refreshDetail();}else await loadUsers();}catch(e){status.textContent=e.message;}finally{busy=false;}}
 search.addEventListener('submit',e=>{e.preventDefault();selected=null;detail.hidden=true;users.hidden=false;refresh();});for(const id of ['activity-refresh','activity-detail-refresh'])document.getElementById(id).addEventListener('click',async e=>{const button=e.currentTarget;button.disabled=true;button.classList.add('is-refreshing');button.setAttribute('aria-busy','true');historyExpanded=false;try{await refresh();}finally{button.disabled=false;button.classList.remove('is-refreshing');button.setAttribute('aria-busy','false');}});
 document.getElementById('activity-back').addEventListener('click',()=>{selected=null;generation++;detail.hidden=true;users.hidden=false;refresh();});
 for(const [id,kind]of [['activity-more-events','events'],['activity-more-sessions','sessions']])document.getElementById(id).addEventListener('click',async e=>{e.target.disabled=true;try{await refreshDetail(kind);}catch(error){status.textContent=error.message;}finally{e.target.disabled=false;}});
 window.addEventListener('teacher-panel-change',e=>{if(e.detail==='activity')refresh();else{generation++;users.replaceChildren();document.getElementById('activity-sessions').replaceChildren();document.getElementById('activity-events').replaceChildren();selected=null;detail.hidden=true;users.hidden=false;}});
 const poll=setInterval(refresh,30000);window.addEventListener('pagehide',()=>clearInterval(poll));
 document.addEventListener('visibilitychange',()=>{if(!document.hidden)refresh();});window.addEventListener('blur',()=>{if(!panel.hidden){historyExpanded=false;generation++;users.replaceChildren();document.getElementById('activity-sessions').replaceChildren();document.getElementById('activity-events').replaceChildren();}});window.addEventListener('focus',refresh);
})();
