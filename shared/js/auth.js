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
  try{const url=new URL(value||'/exercicios/index.html',location.origin);if(url.origin===location.origin&&(url.pathname.startsWith('/exercicios/')||url.pathname.startsWith('/apoio/')||url.pathname.startsWith('/entregas/')||url.pathname.startsWith('/amigos/')||url.pathname.startsWith('/mensagens/')))return url.pathname+url.search;}catch{}
  return '/exercicios/index.html';
 }
 async function logout(){csrfToken=null;await request('/api/auth/logout',{method:'POST',body:{}});location.assign('/login/index.html');}

 let profile=null,openProfile,openPrivacy;
 function node(tag,text,cls){const n=document.createElement(tag);if(text!==undefined)n.textContent=text;if(cls)n.className=cls;return n;}
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
  const menuMotion=new WeakMap();
  const reducedMotion=matchMedia('(prefers-reduced-motion: reduce)');
  function expanded(menu,value){(menu===nav?navButton:photo)?.setAttribute('aria-expanded',String(value));}
  function closedFrame(menu){return menu===settings?{opacity:0,transform:'translateY(-18px) scaleY(.9)',clipPath:'inset(0% 0% 100% 0% round 12px)'}:{opacity:0,transform:'scaleX(.82)',clipPath:'inset(0% 0% 0% 100% round 12px)'};}
  const openedFrame={opacity:1,transform:'none',clipPath:'inset(0% 0% 0% 0% round 12px)'};
  function cancelMotion(menu){for(const animation of menu.getAnimations({subtree:true}))animation.cancel();menuMotion.delete(menu);}
  function closeMenu(menu){
   if(menu.hidden||menu.dataset.motionState==='closing')return;
   const style=getComputedStyle(menu),start={opacity:style.opacity,transform:style.transform,clipPath:style.clipPath==='none'?openedFrame.clipPath:style.clipPath};
   cancelMotion(menu);expanded(menu,false);menu.inert=true;menu.dataset.motionState='closing';
   if(reducedMotion.matches){menu.hidden=true;menu.inert=false;delete menu.dataset.motionState;return;}
   const animation=menu.animate([start,closedFrame(menu)],{duration:220,easing:'cubic-bezier(.4,0,.6,1)',fill:'both'});menuMotion.set(menu,animation);
   animation.onfinish=()=>{if(menuMotion.get(menu)!==animation)return;menu.hidden=true;menu.inert=false;delete menu.dataset.motionState;cancelMotion(menu);};
  }
  function hide(){closeMenu(settings);closeMenu(nav);}
  function toggle(menu,button){
   if(!menu.hidden&&menu.dataset.motionState!=='closing'){closeMenu(menu);return;}
   closeMenu(menu===nav?settings:nav);cancelMotion(menu);menu.hidden=false;menu.inert=false;expanded(menu,true);menu.dataset.motionState='opening';
   if(reducedMotion.matches){menu.dataset.motionState='open';return;}
   const animation=menu.animate([closedFrame(menu),openedFrame],{duration:460,easing:'cubic-bezier(.22,.61,.36,1)',fill:'both'});menuMotion.set(menu,animation);
   [...menu.querySelectorAll('.menu-option,.popover-label,.theme-switch')].forEach((item,index)=>item.animate([{opacity:0,transform:menu===settings?'translateY(-12px)':'translateX(14px)'},{opacity:1,transform:'none'}],{duration:280,delay:60+Math.min(index*22,110),easing:'ease-out',fill:'backwards'}));
   animation.onfinish=()=>{if(menuMotion.get(menu)!==animation)return;menu.dataset.motionState='open';cancelMotion(menu);};
  }
  navButton.addEventListener('click',()=>toggle(nav,navButton));photo?.addEventListener('click',()=>toggle(settings,photo));
  document.addEventListener('click',e=>{if(!outer.contains(e.target))hide();});document.addEventListener('keydown',e=>{if(e.key==='Escape')hide();});
  for(const [label,url,cls]of [['Menu principal','/index.html',''],['Exercícios','/exercicios/index.html','nav-exercises'],['Apoio','/apoio/index.html','nav-support'],['Contato','/contato/index.html','nav-contact-green']]){const link=node('a',label,'menu-option '+cls);link.href=url;nav.append(link);}
  if(profile){for(const [label,url]of [['Amigos','/amigos/index.html'],['Mensagens','/mensagens/index.html']]){const social=node('a',label,'menu-option');social.href=url;nav.append(social);}const deliveries=node('a',profile.role==='admin'?'Painel do professor':'Minhas entregas','menu-option');const ex=location.pathname.match(/\/exercicios\/exercicio-(\d+)\//);deliveries.href=profile.role==='admin'?'/alunos/index.html':'/entregas/index.html'+(ex?'?exercicio='+ex[1]:'');nav.append(deliveries);}
  else {const login=node('a','Entrar / criar conta','menu-option');login.href='/login/index.html';nav.append(login);}
  function dialog(title,content){const d=node('dialog',undefined,'settings-dialog');d.innerHTML=content;d.setAttribute('aria-label',title);const top=node('div',undefined,'dialog-top');top.append(node('h2',title));const close=option('×',()=>d.close(),top,'dialog-close');close.setAttribute('aria-label','Fechar configurações');d.prepend(top);document.body.append(d);d.addEventListener('click',e=>{if(e.target===d)d.close();});return d;}
  if(profile){
   const profileDialog=dialog('Meu perfil',`<form id="shell-profile-form"><div id="profile-photo-preview"></div><label>Nome<input name="name" maxlength="100" required></label><label>Biografia<textarea name="biography" maxlength="600" rows="4" placeholder="Conte um pouco sobre você…"></textarea></label><label>Foto de perfil<input name="photo" type="file" accept="image/jpeg,image/png,image/webp"></label><label class="check-label"><input name="removeAvatar" type="checkbox"><span>Remover minha foto enviada</span></label><label class="check-label"><input name="discoverable" type="checkbox"><span>Permitir que outras pessoas do site me encontrem pelo nome e enviem pedidos de amizade.</span></label><p class="auth-note">Amigos veem sua biografia e foto. E-mails e entregas não são compartilhados com amigos.</p><label class="check-label updated-terms"><input name="acceptTerms" type="checkbox"><span>Li e aceito os <a href="/termos/index.html" target="_blank" rel="noopener">termos atualizados</a>.</span></label><p class="settings-message" role="status"></p><button type="submit" class="auth-submit">Salvar perfil</button></form>`);
   const privacyDialog=dialog('Preferências e privacidade',`<form id="shell-privacy-form"><p>Suas entregas começam privadas. Você escolhe se o professor pode consultá-las e escrever feedback.</p><label class="check-label"><input name="shareWithTeacher" type="checkbox"><span>Permitir acesso do professor às minhas entregas.</span></label><p class="auth-note">Você pode revogar quando quiser. A revogação impede novas consultas; não desfaz informações já lidas.</p><label class="check-label updated-terms"><input name="acceptTerms" type="checkbox"><span>Li e aceito os <a href="/termos/index.html" target="_blank" rel="noopener">termos atualizados</a>.</span></label><p class="settings-message" role="status"></p><button type="submit" class="auth-submit">Salvar preferências</button></form>`);
   const form=profileDialog.querySelector('form'),privacyForm=privacyDialog.querySelector('form');let pendingAvatar=null;
   function refreshForms(){form.elements.name.value=profile.name;form.elements.biography.value=profile.biography;form.elements.discoverable.checked=profile.discoverable;form.elements.removeAvatar.checked=false;form.elements.photo.value='';pendingAvatar=null;profileDialog.querySelector('#profile-photo-preview').replaceChildren(avatar(profile,'profile-preview'));privacyForm.elements.shareWithTeacher.checked=profile.shareWithTeacher;for(const d of [profileDialog,privacyDialog]){const checkbox=d.querySelector('[name="acceptTerms"]');checkbox.required=!profile.acceptedTerms;checkbox.checked=false;d.querySelector('.updated-terms').hidden=profile.acceptedTerms;d.querySelector('.settings-message').textContent='';}}
   async function reload(){profile=await request('/api/profile');photo?.replaceChildren(avatar(profile));refreshForms();window.dispatchEvent(new CustomEvent('portfolio-profile-change',{detail:profile}));}
   openProfile=()=>{hide();refreshForms();profileDialog.showModal();};openPrivacy=()=>{if(profile.role!=='student')return;hide();refreshForms();privacyDialog.showModal();};
   option('Editar perfil',openProfile,settings);option('Senha e acesso',()=>location.assign('/login/index.html?account=1'),settings);if(profile.role==='student')option('⋯ Preferências / Privacy',openPrivacy,settings);
   if(theme){theme.hidden=false;settings.append(node('p','Tema','popover-label'),theme);}
   option('Sair',()=>logout().catch(e=>alert(e.message)),settings);
   if(!showAvatar){option('Perfil e configurações',openProfile,nav);option('Senha e acesso',()=>location.assign('/login/index.html?account=1'),nav);if(profile.role==='student')option('⋯ Preferências / Privacy',openPrivacy,nav);option('Sair',()=>logout().catch(e=>alert(e.message)),nav);if(theme){settings.removeChild(theme);nav.append(theme);}}
   form.elements.photo.addEventListener('change',async()=>{const file=form.elements.photo.files[0],message=profileDialog.querySelector('.settings-message');if(!file)return;if(!['image/jpeg','image/png','image/webp'].includes(file.type)||file.size>8000000){message.textContent='Escolha uma foto JPEG, PNG ou WebP de até 8 MB.';form.elements.photo.value='';return;}
    try{const bitmap=await createImageBitmap(file,{imageOrientation:'from-image'}),canvas=document.createElement('canvas');canvas.width=canvas.height=256;const ctx=canvas.getContext('2d'),size=Math.min(bitmap.width,bitmap.height);ctx.fillStyle='#fff';ctx.fillRect(0,0,256,256);ctx.drawImage(bitmap,(bitmap.width-size)/2,(bitmap.height-size)/2,size,size,0,0,256,256);bitmap.close();pendingAvatar=canvas.toDataURL('image/jpeg',.82);form.elements.removeAvatar.checked=false;const preview=avatar({...profile,avatar:pendingAvatar},'profile-preview');profileDialog.querySelector('#profile-photo-preview').replaceChildren(preview);message.textContent='Foto pronta para salvar.';}catch{message.textContent='Não foi possível abrir essa foto.';form.elements.photo.value='';}
   });
   form.addEventListener('submit',async e=>{e.preventDefault();const button=form.querySelector('button[type=submit]'),message=form.querySelector('.settings-message');button.disabled=true;try{const data=await request('/api/profile',{method:'POST',body:{name:form.elements.name.value,biography:form.elements.biography.value,avatar:pendingAvatar,removeAvatar:form.elements.removeAvatar.checked,discoverable:form.elements.discoverable.checked,acceptTerms:form.elements.acceptTerms.checked}});await reload();message.textContent=data.message;}catch(e){message.textContent=e.message;}finally{button.disabled=false;}});
   privacyForm.addEventListener('submit',async e=>{e.preventDefault();const button=privacyForm.querySelector('button'),message=privacyForm.querySelector('.settings-message');button.disabled=true;try{const data=await request('/api/profile/privacy',{method:'POST',body:{shareWithTeacher:privacyForm.elements.shareWithTeacher.checked,acceptTerms:privacyForm.elements.acceptTerms.checked}});await reload();message.textContent=data.message;}catch(e){message.textContent=e.message;}finally{button.disabled=false;}});
  }else if(theme){theme.hidden=false;nav.append(node('p','Tema','popover-label'),theme);}
  outer.classList.add('shell-ready');return profile;
 }
 const ready=controls();return {request,returnPath,logout,ready,openProfile:()=>openProfile?.(),openPrivacy:()=>openPrivacy?.(),resetCsrf:()=>{csrfToken=null;}};
})();
