"use strict";
const $=selector=>document.querySelector(selector);
const signals = {
  metrics: { label: "ACOMPANHAR", text: "Números que ajudam a acompanhar o sistema, como tempo de resposta, uso de memória e quantidade de erros." },
  logs: { label: "INVESTIGAR", text: "Registros de eventos que ajudam a entender o que aconteceu na aplicação e a encontrar pistas sobre um problema." },
  traces: { label: "CONECTAR", text: "O caminho de uma requisição entre serviços, mostrando onde ela passou e quanto tempo cada etapa levou." }
};
const tabs = [...document.querySelectorAll("[data-signal]")];
function selectSignal(tab, focus = false) {
  tabs.forEach((item) => {
    const selected = item === tab;
    item.setAttribute("aria-selected", String(selected));
    item.tabIndex = selected ? 0 : -1;
  });
  const signal = signals[tab.dataset.signal];
  $("#signal-label").textContent = signal.label;
  $("#signal-description").textContent = signal.text;
  $("#signal-panel").setAttribute("aria-labelledby", tab.id);
  if (focus) tab.focus();
}
tabs.forEach((tab, index) => {
  tab.addEventListener("click", () => selectSignal(tab));
  tab.addEventListener("keydown", (event) => {
    let next;
    if (event.key === "ArrowRight") next = (index + 1) % tabs.length;
    if (event.key === "ArrowLeft") next = (index + tabs.length - 1) % tabs.length;
    if (event.key === "Home") next = 0;
    if (event.key === "End") next = tabs.length - 1;
    if (next !== undefined) { event.preventDefault(); selectSignal(tabs[next], true); }
  });
});

