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
  try{const url=new URL(value||'/exercicios/index.html',location.origin);if(url.origin===location.origin&&(url.pathname.startsWith('/exercicios/')||url.pathname.startsWith('/apoio/')))return url.pathname+url.search;}catch{}
  return '/exercicios/index.html';
 }
 async function logout(){csrfToken=null;await request('/api/auth/logout',{method:'POST',body:{}});location.assign('/login/index.html');}
 async function controls(){
  const header=document.querySelector('.header-inner')||document.querySelector('header');if(!header)return;
  const slot=document.createElement('div');slot.className='account-controls';header.append(slot);
  let user;try{user=await request('/api/auth/me');}catch{}
  const link=document.createElement('a');link.className='account-link';link.href=user?'/login/index.html?account=1':'/login/index.html';link.textContent=user?'Minha conta':'Entrar';slot.append(link);
  if(user){if(user.role==='admin'){const panel=document.createElement('a');panel.className='account-link';panel.href='/alunos/index.html';panel.textContent='Alunos';slot.append(panel);}const exit=document.createElement('button');exit.type='button';exit.className='account-link';exit.textContent='Sair';exit.addEventListener('click',()=>logout().catch(e=>alert(e.message)));slot.append(exit);}
 }
 controls();return {request,returnPath,logout,resetCsrf:()=>{csrfToken=null;}};
})();
