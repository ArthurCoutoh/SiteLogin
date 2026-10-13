"use strict";
(() => {const config=window.PORTFOLIO_CONFIG||{};const now=new Date();let age=now.getFullYear()-2008;if(now.getMonth()<7||(now.getMonth()===7&&now.getDate()<18))age--;document.querySelectorAll("[data-age]").forEach(e=>e.textContent=String(age));document.querySelectorAll("[data-year]").forEach(e=>e.textContent=String(now.getFullYear()));const phone=String(config.whatsapp||"5569993644926").replace(/\D/g,"");document.querySelectorAll(".whatsapp-link").forEach(e=>e.href=`https://wa.me/${phone}`);const instagram=String(config.instagram||"arthurcoutoh").replace(/^@/,"");document.querySelectorAll(".instagram-link").forEach(e=>e.href=`https://www.instagram.com/${encodeURIComponent(instagram)}/`);const pl=document.getElementById("phone-label"),il=document.getElementById("instagram-label");if(pl)pl.textContent=config.numeroExibido||"(69) 99364-4926";if(il)il.textContent=`@${instagram}`;})();
let toastTimer;
window.portfolioToast=message=>{const toast=document.getElementById("toast");if(!toast)return;clearTimeout(toastTimer);toast.textContent=message;toast.classList.add("show");toastTimer=setTimeout(()=>toast.classList.remove("show"),4000);};

// One theme preference is shared by every portfolio page.
(() => {
 const header=document.querySelector('header');if(!header)return;
 const control=document.createElement('div');control.className='theme-switch';control.hidden=true;control.setAttribute('role','group');control.setAttribute('aria-label','Color theme');
 for(const theme of ['light','dark']){
  const button=document.createElement('button');button.type='button';button.textContent=theme==='light'?'Light':'Dark';button.dataset.themeChoice=theme;
  button.addEventListener('click',()=>{document.documentElement.dataset.theme=theme;try{localStorage.setItem('portfolio-theme',theme);}catch(e){}sync();window.dispatchEvent(new Event('portfolio-theme-change'));});control.append(button);
 }
 (header.querySelector('.header-inner')||header).append(control);
 function sync(){const theme=document.documentElement.dataset.theme||'dark';control.querySelectorAll('button').forEach(b=>b.setAttribute('aria-pressed',String(b.dataset.themeChoice===theme)));const meta=document.querySelector('meta[name="theme-color"]');if(meta)meta.content=theme==='light'?'#ffffff':'#000000';}
 sync();
})();
