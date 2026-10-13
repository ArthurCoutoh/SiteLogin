"use strict";
// Keyboard shortcuts for the visible menu cards.
document.querySelectorAll(".menu-card").forEach((card, index) => { card.setAttribute("aria-keyshortcuts", `Alt+${index + 1}`); });
document.addEventListener("keydown", event => { if(event.altKey && /^[1-5]$/.test(event.key)){const card=document.querySelectorAll(".menu-card")[Number(event.key)-1];if(card){event.preventDefault();card.click();}} });
