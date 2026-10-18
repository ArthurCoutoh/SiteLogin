"use strict";
window.PortfolioAuth=(()=>{
 let csrfToken;
 async function request(path,{method="GET",body}={}){
  const headers={};
  if(method!=="GET"){
   if(!csrfToken){const r=await fetch('/api/auth/csrf',{credentials:'same-origin',cache:'no-store'});if(!r.ok)throw new Error('Não foi possível conectar ao login.');csrfToken=(await r.json()).token;}
   headers['X-CSRF-TOKEN']=csrfToken;headers['Content-Type']='application/json';
  }
  const response=await fetch(path,{method,credentials:'same-origin',cache:'no-store',headers,body:body===undefined?undefined:JSON.stringify(body)});
  let data;try{data=await response.json();}catch{throw new Error('O sistema de login precisa ser executado pelo servidor.');}
  if(!response.ok){const error=new Error(data.message||(response.status===429?'Muitas tentativas. Aguarde um minuto.':'Não foi possível concluir.'));error.status=response.status;throw error;}
  return data;
 }
 function returnPath(){
  const value=new URLSearchParams(location.search).get('returnUrl');
  try{const url=new URL(value||'/exercicios/index.html',location.origin);if(url.origin===location.origin&&(url.pathname.startsWith('/exercicios/')||url.pathname.startsWith('/apoio/')||url.pathname.startsWith('/entregas/')||url.pathname.startsWith('/amigos/')||url.pathname.startsWith('/mensagens/')||url.pathname.startsWith('/perfil/')))return url.pathname+url.search;}catch{}
  return '/exercicios/index.html';
 }
 async function logout(){csrfToken=null;await request('/api/auth/logout',{method:'POST',body:{}});location.assign('/login/index.html');}

 let profile=null,openProfile,openPrivacy;
 function node(tag,text,cls){const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n;}
 function showPrivacyConfirmation(text='Entrega realizada'){
  document.querySelector('.privacy-confirmation')?.remove();
  const notice=node('div',text,'privacy-confirmation');notice.setAttribute('role','status');notice.setAttribute('aria-live','polite');document.body.append(notice);
  const animation=notice.animate([
   {transform:'translate(-50%, -150%)',opacity:0,offset:0,easing:'cubic-bezier(.2,.7,.2,1)'},
   {transform:'translate(-50%, 0)',opacity:1,offset:600/2100},
   {transform:'translate(-50%, 0)',opacity:1,offset:1600/2100,easing:'cubic-bezier(.4,0,.8,.3)'},
   {transform:'translate(-50%, -150%)',opacity:0,offset:1}
  ],{duration:2100,fill:'both'});animation.onfinish=()=>notice.remove();
 }
 function avatar(user,cls='profile-avatar'){const img=node('img',undefined,cls);img.alt='';img.src=user.avatar||(user.role==='admin'?'/assets/perfil.jpg':'/shared/avatar.svg');return img;}
 function option(label,action,container,cls='menu-option'){const b=node('button',label,cls);b.type='button';b.addEventListener('click',action);container.append(b);return b;}
 async function controls(){
  const outer=document.querySelector('header'),header=outer?.querySelector('.header-inner')||outer;if(!header)return null;
  document.body.classList.add('shell-clean');outer.classList.add('clean-header');
  const theme=header.querySelector('.theme-switch')||outer.querySelector('.theme-switch');
  try{profile=await request('/api/profile');}catch{}
  const isStudy=location.pathname.startsWith('/exercicios/')||location.pathname.startsWith('/apoio/');
  const isDetail=/\/exercicios\/exercicio-\d+\//.test(location.pathname)||/\/apoio\/[^/]+\//.test(location.pathname);
  const showAvatar=profile&&!isDetail&&!location.pathname.startsWith('/login/')&&!location.pathname.startsWith('/termos/');
  const studyNav=isStudy?header.querySelector('nav'):null;if(studyNav){for(const a of studyNav.querySelectorAll('a')){const url=new URL(a.href);if(url.pathname!=='/index.html'&&!url.pathname.startsWith('/exercicios/')&&!url.pathname.startsWith('/apoio/'))a.remove();}}
  header.replaceChildren();const left=node('div',undefined,'shell-left');header.append(left);
  const settings=node('div',undefined,'profile-popover shell-popover');settings.hidden=true;settings.id='profile-popover';
  let photo;
  if(showAvatar){photo=node('button',undefined,'avatar-trigger');photo.type='button';photo.setAttribute('aria-label','Abrir perfil e configurações');photo.setAttribute('aria-expanded','false');photo.setAttribute('aria-controls',settings.id);photo.append(avatar(profile));left.append(photo);}
  else if(!studyNav){const brand=node('a','AC.','shell-brand');brand.href='/index.html';brand.setAttribute('aria-label','Menu principal');left.append(brand);}
  if(studyNav)left.append(studyNav);
  const navButton=node('button','☰','hamburger-trigger');navButton.type='button';navButton.setAttribute('aria-label','Abrir navegação');navButton.setAttribute('aria-expanded','false');navButton.setAttribute('aria-controls','navigation-popover');header.append(navButton);
  const nav=node('div',undefined,'navigation-popover shell-popover');nav.id='navigation-popover';nav.hidden=true;outer.append(settings,nav);
  const headerActions=node('div',undefined,'header-actions');header.append(headerActions);headerActions.append(navButton);
  const bell=node('button',undefined,'notification-trigger');bell.type='button';bell.setAttribute('aria-label','Notificações');bell.setAttribute('aria-expanded','false');bell.setAttribute('aria-controls','notification-popover');
  bell.innerHTML='<svg viewBox="0 0 24 24" aria-hidden="true"><path d="M18 8a6 6 0 0 0-12 0c0 7-3 7-3 9h18c0-2-3-2-3-9"/><path d="M10 21h4M12 2V1"/></svg>';
  const notifications=node('div',undefined,'notification-popover shell-popover');notifications.id='notification-popover';notifications.hidden=true;notifications.setAttribute('aria-label','Notificações');
  notifications.append(node('h2','Notificações','popover-label'));
  const notificationList=node('div',undefined,'notification-list');notificationList.setAttribute('aria-live','polite');notifications.append(notificationList);
  const badge=node('span',undefined,'notification-count');badge.hidden=true;badge.setAttribute('aria-hidden','true');bell.append(badge);
  let notificationBusy=false,notificationReadPromise=null,notificationItems=[];const notificationKnownRead=new Set();
  function markAllNotificationsRead(){
   if(notificationReadPromise)return notificationReadPromise;
   if(!notificationItems.some(x=>!x.read))return Promise.resolve();
   notificationReadPromise=(async()=>{
    const data=await request('/api/notifications/read-all',{method:'POST',body:{}});const ids=new Set(data.ids);for(const id of ids)notificationKnownRead.add(id);
    for(const item of notificationItems)if(ids.has(item.id))item.read=true;
    for(const entry of notificationList.querySelectorAll('.notification-entry'))if(ids.has(entry.dataset.notificationId)){
     entry.classList.remove('is-unread');entry.querySelector('.notification-item')?.classList.remove('is-unread');
     const dot=entry.querySelector('.notification-unread-dot');if(dot)dot.hidden=true;
     const mark=entry.querySelector('.notification-mark-read');if(mark){mark.disabled=true;mark.textContent='Lida';mark.setAttribute('aria-label','Notificação lida');}
    }
    const remaining=notificationItems.filter(x=>!x.read).length;badge.hidden=!remaining;badge.textContent=remaining>99?'99+':String(remaining);bell.setAttribute('aria-label',remaining?'Notificações, '+remaining+' não lidas':'Notificações');
   })().finally(()=>{notificationReadPromise=null;});return notificationReadPromise;
  }
  function autoReadNotifications(){markAllNotificationsRead().catch(error=>{let state=notificationList.querySelector('.notification-error');if(!state){state=node('p','','notification-note notification-error');state.setAttribute('role','status');notificationList.append(state);}state.textContent=error.message;});}

  async function loadNotifications(){
   if(notificationBusy||notificationReadPromise||document.hidden)return;notificationBusy=true;
   try{
    if(!profile){notificationList.replaceChildren();const login=node('a','Entre para ver suas notificações','menu-option');login.href='/login/index.html';notificationList.append(login);return;}
    const items=await request('/api/notifications');for(const item of items)if(notificationKnownRead.has(item.id))item.read=true;notificationItems=items;notificationList.replaceChildren();
    const unread=items.filter(item=>!item.read).length;badge.hidden=!unread;badge.textContent=unread>99?'99+':String(unread);bell.setAttribute('aria-label',unread?'Notificações, '+unread+' não lidas':'Notificações');
    if(!items.length)notificationList.append(node('p','Nenhuma notificação por enquanto.','notification-note'));
    for(const item of items){
     const target=new URL(item.url,location.origin);if(target.origin!==location.origin)continue;
     const link=node('a',undefined,'notification-item'+(item.read?'':' is-unread'));link.href=target.pathname+target.search;
     link.append(node('strong',item.title),node('time',new Date(item.createdAt+'Z').toLocaleString('pt-BR',{dateStyle:'short',timeStyle:'short'})));
     link.addEventListener('click',async e=>{if(e.ctrlKey||e.metaKey||e.shiftKey||e.altKey){autoReadNotifications();return;}e.preventDefault();try{await markAllNotificationsRead();}catch{}location.assign(link.href);});
     const entry=node('div',undefined,'notification-entry'+(item.read?'':' is-unread'));entry.dataset.notificationId=item.id;entry.append(link);
     const dot=node('span',undefined,'notification-unread-dot');dot.hidden=item.read;dot.setAttribute('aria-label','Não lida');entry.append(dot);
     entry.addEventListener('mouseenter',autoReadNotifications);entry.addEventListener('focusin',autoReadNotifications);
     if(!item.read){const mark=node('button','Marcar como lida','notification-mark-read');mark.type='button';mark.setAttribute('aria-label','Marcar como lida: '+item.title);mark.addEventListener('click',autoReadNotifications);entry.append(mark);}
     else entry.append(node('span','Lida','notification-read-label'));
     notificationList.append(entry);
    }
   }catch{notificationList.replaceChildren(node('p','Não foi possível carregar. Clique no sino para tentar novamente.','notification-note'));}
   finally{notificationBusy=false;}
  }
  loadNotifications();const notificationPoll=setInterval(loadNotifications,20000);document.addEventListener('visibilitychange',()=>{if(!document.hidden)loadNotifications();});window.addEventListener('pagehide',()=>clearInterval(notificationPoll));
  headerActions.prepend(bell);outer.append(notifications);
  const menuMotion=new WeakMap();
  function expanded(menu,value){(menu===nav?navButton:menu===notifications?bell:photo)?.setAttribute('aria-expanded',String(value));}
  function closedFrame(menu){return menu===settings?{opacity:1,transform:'translateY(-48px)',clipPath:'inset(0% 0% 100% 0% round 12px)'}:{opacity:1,transform:'scaleX(.45)',clipPath:'inset(0% 0% 0% 100% round 12px)'};}
  const openedFrame={opacity:1,transform:'none',clipPath:'inset(0% 0% 0% 0% round 12px)'};
  function cancelMotion(menu){for(const animation of menu.getAnimations({subtree:true}))animation.cancel();menuMotion.delete(menu);}
  function closeMenu(menu){
   if(menu.hidden||menu.dataset.motionState==='closing')return;
   const style=getComputedStyle(menu),start={opacity:style.opacity,transform:style.transform,clipPath:style.clipPath==='none'?openedFrame.clipPath:style.clipPath};
   cancelMotion(menu);expanded(menu,false);menu.inert=true;menu.dataset.motionState='closing';
   const animation=menu.animate([start,closedFrame(menu)],{duration:menu===notifications?400:600,easing:'cubic-bezier(.4,0,.2,1)',fill:'both'});menuMotion.set(menu,animation);
   animation.onfinish=()=>{if(menuMotion.get(menu)!==animation)return;menu.hidden=true;menu.inert=false;delete menu.dataset.motionState;cancelMotion(menu);};
  }
  function hide(){closeMenu(settings);closeMenu(nav);closeMenu(notifications);}
  function toggle(menu,button){
   if(!menu.hidden&&menu.dataset.motionState!=='closing'){closeMenu(menu);return;}
   for(const other of [settings,nav,notifications])if(other!==menu)closeMenu(other);cancelMotion(menu);menu.hidden=false;menu.inert=false;expanded(menu,true);menu.dataset.motionState='opening';
   const animation=menu.animate([closedFrame(menu),openedFrame],{duration:600,easing:'cubic-bezier(.4,0,.2,1)',fill:'both'});menuMotion.set(menu,animation);
   [...menu.querySelectorAll('.menu-option,.popover-label,.theme-switch')].forEach((item,index)=>item.animate([{opacity:0,transform:menu===settings?'translateY(-24px)':'translateX(42px)'},{opacity:1,transform:'none'}],{duration:360,delay:60+Math.min(index*30,150),easing:'ease-in-out',fill:'backwards'}));
   animation.onfinish=()=>{if(menuMotion.get(menu)!==animation)return;menu.dataset.motionState='open';cancelMotion(menu);};
  }
  bell.addEventListener('click',()=>{toggle(notifications,bell);if(!notifications.hidden)loadNotifications();});navButton.addEventListener('click',()=>toggle(nav,navButton));photo?.addEventListener('click',()=>toggle(settings,photo));
  document.addEventListener('click',e=>{if(!outer.contains(e.target))hide();});document.addEventListener('keydown',e=>{if(e.key==='Escape')hide();});
  for(const [label,url,cls]of [['Menu principal','/index.html',''],['Exercícios','/exercicios/index.html','nav-exercises'],['Apoio','/apoio/index.html','nav-support'],['Contato','/contato/index.html','nav-contact-green']]){const link=node('a',label,'menu-option '+cls);link.href=url;nav.append(link);}
  if(profile){for(const [label,url]of [['Amigos','/amigos/index.html'],['Mensagens','/mensagens/index.html']]){const social=node('a',label,'menu-option');social.href=url;nav.append(social);}const deliveries=node('a',profile.role==='admin'?'Painel do professor':'Minhas entregas','menu-option');const ex=location.pathname.match(/\/exercicios\/exercicio-(\d+)\//);deliveries.href=profile.role==='admin'?'/alunos/index.html':'/entregas/index.html'+(ex?'?exercicio='+ex[1]:'');nav.append(deliveries);}
  else {const login=node('a','Entrar / criar conta','menu-option');login.href='/login/index.html';nav.append(login);}
  function dialog(title,content){const d=node('dialog',undefined,'settings-dialog');d.innerHTML=content;d.setAttribute('aria-label',title);const top=node('div',undefined,'dialog-top');top.append(node('h2',title));const close=option('×',()=>d.close(),top,'dialog-close');close.setAttribute('aria-label','Fechar configurações');d.prepend(top);document.body.append(d);d.addEventListener('click',e=>{if(e.target===d)d.close();});return d;}
  if(profile){
   const profileDialog=dialog('Meu perfil',`<form id="shell-profile-form"><div id="profile-photo-preview"></div><label>Nome<input name="name" maxlength="100" required></label><label>Biografia<textarea name="biography" maxlength="600" rows="4" placeholder="Conte um pouco sobre você…"></textarea></label><label>Foto de perfil<input name="photo" type="file" accept="image/jpeg,image/png,image/webp"></label><label class="check-label"><input name="removeAvatar" type="checkbox"><span>Remover minha foto enviada</span></label><label class="check-label"><input name="discoverable" type="checkbox"><span>Permitir que outras pessoas do site me encontrem pelo nome e enviem pedidos de amizade.</span></label><p class="auth-note">Amigos veem sua biografia e foto. E-mails e entregas não são compartilhados com amigos.</p><label class="check-label updated-terms"><input name="acceptTerms" type="checkbox"><span>Li e aceito os <a href="/termos/index.html" target="_blank" rel="noopener">termos atualizados</a>.</span></label><p class="settings-message" role="status"></p><button type="submit" class="auth-submit">Salvar perfil</button></form>`);
   const privacyDialog=dialog('Preferências e privacidade',`<form id="shell-privacy-form"><p class="auth-note">O professor pode consultar registros de login, saída e páginas acessadas dos últimos 90 dias. Conteúdo de mensagens e senhas não entra nesses registros.</p><p>Visibilidade das entregas</p><p>Escolha quem pode consultar seus códigos e dar feedback.</p><label class="check-label"><input name="shareWithTeacher" type="checkbox"><span>Professores podem ver minhas entregas.</span></label><label class="check-label"><input name="shareWithStudents" type="checkbox"><span>Alunos podem ver minhas entregas e escrever feedback.</span></label><p class="auth-note">Você pode revogar quando quiser. A revogação impede novas consultas; não desfaz informações já lidas.</p><label class="check-label updated-terms"><input name="acceptTerms" type="checkbox"><span>Li e aceito os <a href="/termos/index.html" target="_blank" rel="noopener">termos atualizados</a>.</span></label><p class="settings-message" role="status"></p><button type="submit" class="auth-submit">Salvar preferências</button></form>`);
   const form=profileDialog.querySelector('form'),privacyForm=privacyDialog.querySelector('form');let pendingAvatar=null;
   function refreshForms(){form.elements.name.value=profile.name;form.elements.biography.value=profile.biography;form.elements.discoverable.checked=profile.discoverable;form.elements.removeAvatar.checked=false;form.elements.photo.value='';pendingAvatar=null;profileDialog.querySelector('#profile-photo-preview').replaceChildren(avatar(profile,'profile-preview'));privacyForm.elements.shareWithTeacher.checked=profile.shareWithTeacher;privacyForm.elements.shareWithStudents.checked=profile.shareWithStudents===true;for(const d of [profileDialog,privacyDialog]){const checkbox=d.querySelector('[name="acceptTerms"]');const isAdmin=profile.role==='admin';checkbox.required=!isAdmin&&!profile.acceptedTerms;checkbox.disabled=isAdmin;checkbox.checked=false;d.querySelector('.updated-terms').hidden=isAdmin||profile.acceptedTerms;d.querySelector('.settings-message').textContent='';}}
   async function reload(){profile=await request('/api/profile');photo?.replaceChildren(avatar(profile));refreshForms();window.dispatchEvent(new CustomEvent('portfolio-profile-change',{detail:profile}));}
   openProfile=()=>{hide();refreshForms();profileDialog.showModal();};openPrivacy=()=>{if(profile.role!=='student')return;hide();refreshForms();privacyDialog.showModal();};
   const viewProfile=node('a','Ver meu perfil','menu-option');viewProfile.href='/perfil/index.html?usuario='+profile.id;settings.append(viewProfile);option('Editar perfil',openProfile,settings);option('Senha e acesso',()=>location.assign('/login/index.html?account=1'),settings);if(profile.role==='student')option('⋯ Preferências / Privacy',openPrivacy,settings);
   if(theme){theme.hidden=false;settings.append(node('p','Tema','popover-label'),theme);}
   option('Sair',()=>logout().catch(e=>alert(e.message)),settings);
   if(!showAvatar){option('Perfil e configurações',openProfile,nav);option('Senha e acesso',()=>location.assign('/login/index.html?account=1'),nav);if(profile.role==='student')option('⋯ Preferências / Privacy',openPrivacy,nav);option('Sair',()=>logout().catch(e=>alert(e.message)),nav);if(theme){settings.removeChild(theme);nav.append(theme);}}
   form.elements.photo.addEventListener('change',async()=>{const file=form.elements.photo.files[0],message=profileDialog.querySelector('.settings-message');if(!file)return;if(!['image/jpeg','image/png','image/webp'].includes(file.type)||file.size>8000000){message.textContent='Escolha uma foto JPEG, PNG ou WebP de até 8 MB.';form.elements.photo.value='';return;}
    try{const bitmap=await createImageBitmap(file,{imageOrientation:'from-image'}),canvas=document.createElement('canvas');canvas.width=canvas.height=256;const ctx=canvas.getContext('2d'),size=Math.min(bitmap.width,bitmap.height);ctx.fillStyle='#fff';ctx.fillRect(0,0,256,256);ctx.drawImage(bitmap,(bitmap.width-size)/2,(bitmap.height-size)/2,size,size,0,0,256,256);bitmap.close();pendingAvatar=canvas.toDataURL('image/jpeg',.82);form.elements.removeAvatar.checked=false;const preview=avatar({...profile,avatar:pendingAvatar},'profile-preview');profileDialog.querySelector('#profile-photo-preview').replaceChildren(preview);message.textContent='Foto pronta para salvar.';}catch{message.textContent='Não foi possível abrir essa foto.';form.elements.photo.value='';}
   });
   form.addEventListener('submit',async e=>{e.preventDefault();const button=form.querySelector('button[type=submit]'),message=form.querySelector('.settings-message');button.disabled=true;message.textContent='';profileDialog.close();try{await request('/api/profile',{method:'POST',body:{name:form.elements.name.value,biography:form.elements.biography.value,avatar:pendingAvatar,removeAvatar:form.elements.removeAvatar.checked,discoverable:form.elements.discoverable.checked,acceptTerms:form.elements.acceptTerms.checked}});showPrivacyConfirmation('Alteração realizada');await reload();}catch(e){message.textContent=e.message;if(!profileDialog.open)profileDialog.showModal();}finally{button.disabled=false;}});
   privacyForm.addEventListener('submit',async e=>{e.preventDefault();const button=privacyForm.querySelector('button'),message=privacyForm.querySelector('.settings-message');button.disabled=true;message.textContent='';privacyDialog.close();try{await request('/api/profile/privacy',{method:'POST',body:{shareWithTeacher:privacyForm.elements.shareWithTeacher.checked,shareWithStudents:privacyForm.elements.shareWithStudents.checked,acceptTerms:privacyForm.elements.acceptTerms.checked}});showPrivacyConfirmation();await reload();}catch(e){message.textContent=e.message;if(!privacyDialog.open)privacyDialog.showModal();}finally{button.disabled=false;}});
  }else if(theme){theme.hidden=false;nav.append(node('p','Tema','popover-label'),theme);}
  const exerciseMatch=location.pathname.match(/\/exercicios\/exercicio-(\d+)\//);
  const documentCard=document.querySelector('.exercise-document');
  if(profile?.role==='student'&&exerciseMatch&&documentCard){
   const action=node('div',undefined,'exercise-submit-action');
   const link=node('a','Enviar minha solução','nav-button nav-exercises');
   link.href='/entregas/index.html?exercicio='+Number(exerciseMatch[1]);
   action.append(link);documentCard.append(action);
  }
  outer.classList.add('shell-ready');return profile;
 }
 const ready=controls();
 let presenceBusy=false,lastPresence=0;
 async function presence(){
  if(document.hidden||presenceBusy||Date.now()-lastPresence<10000)return;
  const user=await ready;if(!user)return;
  presenceBusy=true;lastPresence=Date.now();try{await request('/api/activity/presence',{method:'POST',body:{}});}catch{}finally{presenceBusy=false;}
 }
 ready.then(()=>presence()).catch(()=>{});
 const presencePoll=setInterval(presence,30000);
 document.addEventListener('visibilitychange',()=>{if(!document.hidden)presence();});
 window.addEventListener('pagehide',()=>clearInterval(presencePoll));
 return {request,returnPath,logout,ready,openProfile:()=>openProfile?.(),openPrivacy:()=>openPrivacy?.(),resetCsrf:()=>{csrfToken=null;}};
})();
