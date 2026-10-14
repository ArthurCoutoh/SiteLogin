"use strict";
(async()=>{
 const api=window.PortfolioAuth,status=document.getElementById('profile-status'),list=document.getElementById('shared-submissions'),params=new URLSearchParams(location.search);let viewer,profile;
 const el=(tag,text,cls)=>{const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n;};
 function tab(value){for(const b of document.querySelectorAll('[data-profile-tab]'))b.setAttribute('aria-selected',String(b.dataset.profileTab===value));document.getElementById('profile-about').hidden=value!=='profile';document.getElementById('profile-submissions').hidden=value!=='submissions';}
 for(const b of document.querySelectorAll('[data-profile-tab]'))b.addEventListener('click',()=>tab(b.dataset.profileTab));
 async function load(){
  profile=await api.request('/api/people/'+Number(params.get('usuario')||viewer.id)+'/profile');
  document.getElementById('public-profile').hidden=false;document.getElementById('public-name').textContent=profile.name;document.getElementById('public-biography').textContent=profile.biography||'';
  const img=document.getElementById('public-avatar');img.src=profile.hasAvatar?'/api/avatars/'+profile.id:'/shared/avatar.svg';img.onerror=()=>{img.onerror=null;img.src='/shared/avatar.svg';};
  const own=document.getElementById('own-privacy');own.hidden=viewer.id!==profile.id||viewer.role!=='student';own.onclick=()=>api.openPrivacy();
  list.replaceChildren();status.textContent='';
  if(!profile.canViewSubmissions){list.append(el('p','As entregas não foram compartilhadas com você.','auth-note'));return;}
  if(!profile.submissions.length)list.append(el('p','Nenhuma entrega por enquanto.','auth-note'));
  for(const row of profile.submissions){
   const card=el('article',undefined,'student-card');card.id='shared-'+row.id;
   card.append(el('p','Exercício '+row.exerciseNumber,'eyebrow'),el('h3',row.title));
   const details=el('details'),pre=el('pre');details.append(el('summary','Ver código'));pre.append(el('code',row.solution));details.append(pre);card.append(details);
   if(row.teacherFeedback){const feedback=el('div',undefined,'feedback-box');feedback.append(el('h4','Feedback do professor'),el('p',row.teacherFeedback));card.append(feedback);}
   const comments=el('div');comments.append(el('h4','Feedbacks dos alunos'));
   const entries=profile.comments.filter(c=>c.submissionId===row.id);
   if(!entries.length)comments.append(el('p','Nenhum feedback de aluno por enquanto.','auth-note'));
   for(const item of entries){const feedback=el('div',undefined,'feedback-box');const author=el('a',item.author);author.href='/perfil/index.html?usuario='+item.authorId;feedback.append(author,el('p',item.content),el('small',new Date(item.createdAt+'Z').toLocaleString('pt-BR')));comments.append(feedback);}
   card.append(comments);
   if(profile.canComment||profile.canTeacherFeedback){const form=el('form'),label=el('label','Seu feedback'),input=el('textarea'),button=el('button','Enviar feedback','auth-submit');input.required=true;input.maxLength=5000;input.rows=4;button.type='submit';label.append(input);const note=el('p','','auth-note');note.setAttribute('role','status');form.append(label,button,note);form.addEventListener('submit',async e=>{e.preventDefault();button.disabled=true;try{await api.request(profile.canTeacherFeedback?'/api/teacher/submissions/'+row.id+'/feedback':'/api/submissions/'+row.id+'/comments',{method:'POST',body:profile.canTeacherFeedback?{feedback:input.value}:{content:input.value}});await load();status.textContent='Feedback enviado.';}catch(error){note.textContent=error.message;}finally{button.disabled=false;}});card.append(form);}
   list.append(card);
  }
  const target=document.getElementById('shared-'+params.get('entrega'));if(target){tab('submissions');target.classList.add('notification-target');target.scrollIntoView({block:'center',behavior:'smooth'});}
 }
 try{viewer=await api.ready;if(!viewer){location.assign('/login/index.html?returnUrl='+encodeURIComponent(location.pathname+location.search));return;}await load();if(params.get('aba')==='submissions')tab('submissions');}catch(e){status.textContent=e.message;}
 window.addEventListener('blur',()=>{list.replaceChildren();});window.addEventListener('focus',()=>{if(viewer)load().catch(e=>status.textContent=e.message);});
 window.addEventListener('portfolio-profile-change',()=>load().catch(e=>status.textContent=e.message));
})();
