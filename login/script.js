"use strict";
(()=>{
 const api=window.PortfolioAuth;const message=document.getElementById('auth-message');
 const login=document.getElementById('login-form'),register=document.getElementById('register-form'),password=document.getElementById('password-form');
 function notify(text,error=false){message.textContent=text;message.classList.toggle('is-error',error);}
 for(const button of document.querySelectorAll('[data-auth-tab]'))button.addEventListener('click',()=>{const mode=button.dataset.authTab;login.hidden=mode!=='login';register.hidden=mode!=='register';document.querySelectorAll('[data-auth-tab]').forEach(b=>b.setAttribute('aria-selected',String(b===button)));notify('');});
 async function submit(form,path,body,after){const button=form.querySelector('button[type="submit"]');button.disabled=true;notify('Aguarde…');try{const data=await api.request(path,{method:'POST',body});form.reset();notify(data.message);if(after)await after(data);}catch(e){notify(e.message,true);}finally{button.disabled=false;}}
 login.addEventListener('submit',e=>{e.preventDefault();const f=new FormData(login);submit(login,'/api/auth/login',{email:f.get('email'),password:f.get('password')},()=>location.assign(api.returnPath()));});
 register.addEventListener('submit',e=>{e.preventDefault();const f=new FormData(register);if(f.get('password')!==f.get('confirm'))return notify('As senhas precisam ser iguais.',true);submit(register,'/api/auth/register',{name:f.get('name'),email:f.get('email'),password:f.get('password')});});
 password.addEventListener('submit',e=>{e.preventDefault();const f=new FormData(password);if(f.get('newPassword')!==f.get('confirm'))return notify('As novas senhas precisam ser iguais.',true);submit(password,'/api/auth/password',{currentPassword:f.get('currentPassword'),newPassword:f.get('newPassword')},()=>{setTimeout(()=>location.assign('/login/index.html'),1200);});});
 api.request('/api/auth/me').then(user=>{login.hidden=true;register.hidden=true;document.querySelector('.auth-tabs').hidden=true;document.getElementById('account-panel').hidden=false;document.getElementById('account-name').textContent=user.name;document.getElementById('student-panel-link').hidden=user.role!=='admin';}).catch(e=>{if(e.status!==401)notify('O login ficará disponível quando a API e o MySQL estiverem configurados no servidor.',true);});
})();
