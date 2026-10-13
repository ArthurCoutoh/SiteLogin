"use strict";
const music=window.PORTFOLIO_CONFIG||{};
document.getElementById("music-artist").textContent=`${music.nomeMusica||"Rolê na City"} - ${music.artistaMusica||"Hungria, Pacificadores e Misael"}`;
if(music.linkMusica){try{const url=new URL(music.linkMusica);if(["https:","http:"].includes(url.protocol))document.getElementById("music-link").href=url.href;}catch{}}
