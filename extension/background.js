// Фоновый скрипт: получает всплески от content-скриптов и пересылает их ядру Clipper.
// (Запросы к 127.0.0.1 идут отсюда, а не со страницы Twitch, чтобы не упираться в CORS.)
importScripts("shared/defaults.js");

chrome.runtime.onMessage.addListener((message) => {
  if (message.type === "spike") sendSpike(message.spike);
});

async function sendSpike(spike) {
  const { coreUrl } = await chrome.storage.sync.get(CLIPPER_DEFAULTS);
  let result;
  try {
    const response = await fetch(`${coreUrl}/api/spikes`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(spike),
    });
    result = response.ok ? "отправлено" : `ошибка ${response.status}`;
  } catch (e) {
    result = "ядро недоступно";
  }
  await addToLog({ ...spike, result });
}

// Последние 10 всплесков — показываются в окне расширения.
async function addToLog(entry) {
  const { log = [] } = await chrome.storage.local.get("log");
  log.unshift(entry);
  await chrome.storage.local.set({ log: log.slice(0, 10) });
}
