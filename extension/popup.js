const fields = Object.keys(CLIPPER_DEFAULTS);
const $ = (id) => document.getElementById(id);

async function init() {
  const settings = await chrome.storage.sync.get(CLIPPER_DEFAULTS);
  for (const key of fields) $(key).value = settings[key];

  // Проверяем, запущено ли ядро.
  try {
    const status = await (await fetch(`${settings.coreUrl}/api/status`)).json();
    const recording = status.recordings.some((r) => r.isActive);
    $("status").textContent = recording
      ? `Ядро работает, идёт запись. Клипов: ${status.clips.length}`
      : "Ядро работает, но запись не идёт (проверьте StreamUrl и streamlink)";
  } catch {
    $("status").textContent = "Ядро не запущено: запустите Clipper.App";
  }

  const { log = [] } = await chrome.storage.local.get("log");
  $("log").innerHTML = "";
  for (const e of log) {
    const li = document.createElement("li");
    const time = new Date(e.timestamp).toLocaleTimeString();
    li.textContent = `${time} ${e.platform}/${e.channel}: ${e.messagesPerSecond.toFixed(1)} сообщ/с — ${e.result}`;
    $("log").appendChild(li);
  }
  if (!log.length) $("log").innerHTML = "<li>пока нет</li>";
}

$("save").addEventListener("click", async () => {
  const values = {};
  for (const key of fields) {
    values[key] = key === "coreUrl" ? $(key).value.replace(/\/$/, "") : Number($(key).value);
  }
  await chrome.storage.sync.set(values);
  $("save").textContent = "Сохранено ✓";
});

$("clipNow").addEventListener("click", async () => {
  const { coreUrl } = await chrome.storage.sync.get(CLIPPER_DEFAULTS);
  try {
    await fetch(`${coreUrl}/api/clip-now`, { method: "POST" });
    $("clipNow").textContent = "✂ Клип поставлен в очередь";
  } catch {
    $("clipNow").textContent = "Ядро недоступно";
  }
});

init();
