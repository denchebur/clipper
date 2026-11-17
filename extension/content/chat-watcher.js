// Общая часть для Twitch и YouTube: следит за появлением новых сообщений в DOM,
// кормит ими детектор и при всплеске отправляет событие в background.js.
//
// platform        — "twitch" / "youtube"
// getChannel()    — имя канала или id видео
// messageSelector — CSS-селектор одного сообщения чата
function startChatWatcher({ platform, getChannel, messageSelector }) {
  // Первые секунды чат подгружает историю — эти сообщения не считаем.
  const IGNORE_FIRST_MS = 5000;
  const loadedAt = Date.now();

  chrome.storage.sync.get(CLIPPER_DEFAULTS, (settings) => {
    const detector = new SpikeDetector(settings);

    const observer = new MutationObserver((mutations) => {
      if (Date.now() - loadedAt < IGNORE_FIRST_MS) return;
      for (const m of mutations) {
        for (const node of m.addedNodes) {
          if (node.nodeType === Node.ELEMENT_NODE && node.matches(messageSelector)) {
            detector.addMessage();
          }
        }
      }
    });
    observer.observe(document.body, { childList: true, subtree: true });

    setInterval(() => {
      const spike = detector.check();
      if (!spike) return;

      const payload = {
        timestamp: new Date().toISOString(),
        platform,
        channel: getChannel(),
        ...spike,
      };
      console.log("[Clipper] всплеск чата", payload);
      chrome.runtime.sendMessage({ type: "spike", spike: payload });
    }, 1000);

    console.log(`[Clipper] слежу за чатом ${platform}/${getChannel()}`);
  });
}
