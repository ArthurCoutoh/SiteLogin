"use strict";
(() => {
 const activities=(Array.isArray(window.ATIVIDADES)?window.ATIVIDADES:[]).filter(a=>a&&typeof a.titulo==="string").map((a,index)=>({...a,number:index+1}));
 const search=document.getElementById("exercise-search"),filter=document.getElementById("exercise-difficulty"),typeFilter=document.getElementById("exercise-type"),list=document.getElementById("exercise-list"),empty=document.getElementById("exercise-empty"),count=document.getElementById("exercise-count"),stage=document.querySelector(".exercise-stage"),pagination=document.getElementById("exercise-pagination");
 const levels={facil:{label:"easy",color:"easy"},medio:{label:"medium",color:"medium"},dificil:{label:"hard",color:"hard"},"muito-dificil":{label:"very hard",color:"very-hard"}};
 const typeLabels={"if-else": "if / else", "switch-case": "switch case", "while": "while", "do-while": "do while", "for": "for", "foreach": "foreach", "vetores": "vetores", "matrizes": "matrizes", "funcoes": "funções", "strings": "strings"};
 const normalize=text=>String(text||"").normalize("NFD").replace(/[\u0300-\u036f]/g,"").toLowerCase().trim();
 const pageSize=6;let currentPage=1;
 function element(tag,className,text){const node=document.createElement(tag);node.className=className;node.textContent=text;return node;}
 function render(focusPage=false){
  const query=normalize(search.value);
  const filtered=activities.filter(a=>(!filter.value||a.dificuldade===filter.value)&&(!typeFilter.value||(a.tipos||[]).includes(typeFilter.value))&&normalize(`${a.titulo} ${a.enunciado||""}`).includes(query));
  const pageCount=Math.ceil(filtered.length/pageSize);currentPage=Math.max(1,Math.min(currentPage,pageCount||1));
  const offset=(currentPage-1)*pageSize,visible=filtered.slice(offset,offset+pageSize);
  list.replaceChildren();pagination.replaceChildren();stage.hidden=!filtered.length;empty.hidden=filtered.length>0;pagination.hidden=pageCount<=1;
  count.textContent=filtered.length?`${filtered.length} exercício${filtered.length===1?"":"s"} · Mostrando ${offset+1}–${offset+visible.length} · Página ${currentPage} de ${pageCount}`:"Nenhum exercício encontrado.";
  empty.querySelector("h2").textContent=activities.length?"Nenhum exercício encontrado.":"Novas atividades em breve.";
  empty.querySelector("p").textContent=activities.length?"Experimente outro termo, dificuldade ou tipo.":"Os exercícios serão disponibilizados aqui pelo professor.";
  for(const activity of visible){
   let url;try{if(activity.pagina){const candidate=new URL(activity.pagina,location.href);if(["file:","http:","https:"].includes(candidate.protocol))url=candidate.href;}}catch{}
   const card=document.createElement(url?"a":"article");card.className="exercise-card";if(url)card.href=url;
   const level=levels[activity.dificuldade]||levels.facil;card.dataset.difficulty=level.color;
   const meta=element("div","exercise-card-meta","");meta.append(element("span","exercise-card-number",String(activity.number).padStart(2,"0")),element("span","exercise-card-difficulty",level.label));
   const tags=element("div","exercise-card-types","");tags.setAttribute("aria-label","Tipos de exercício");for(const type of activity.tipos||[]){tags.append(element("span","exercise-card-type",typeLabels[type]||type));}
   card.append(meta,tags,element("h2","",activity.titulo),element("p","exercise-card-summary",activity.enunciado||""));
   const action=element("span","exercise-card-action","");action.append(element("span","",url?"Ver enunciado completo":"Em preparação"));
   if(url){const arrow=element("span","","↗");arrow.setAttribute("aria-hidden","true");action.append(arrow);}card.append(action);list.append(card);
  }
  function pageButton(text,destination,disabled=false){
   const button=element("button","",text);button.type="button";button.disabled=disabled;
   if(/^\d+$/.test(text)){button.setAttribute("aria-label",`Página ${text}`);if(destination===currentPage)button.setAttribute("aria-current","page");}
   button.addEventListener("click",()=>{currentPage=destination;render(true);});pagination.append(button);
  }
  if(pageCount>1){pageButton("Anterior",currentPage-1,currentPage===1);for(let p=1;p<=pageCount;p++)pageButton(String(p),p);pageButton("Próxima",currentPage+1,currentPage===pageCount);}
  if(focusPage){pagination.querySelector('[aria-current="page"]')?.focus({preventScroll:true});stage.scrollIntoView({block:"start",behavior:"auto"});}
 }
 function reset(){currentPage=1;render();}
 search.addEventListener("input",reset);filter.addEventListener("change",reset);typeFilter.addEventListener("change",reset);render();
})();
