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
    // Статистику считаем с момента, когда начали засчитывать сообщения, иначе пропущенные
    // первые секунды выглядят как «тихий чат» и сразу дают ложный всплеск.
    detector.startedAt = loadedAt + IGNORE_FIRST_MS;

    const observer = new MutationObserver((mutations) => {
      if (Date.now() - loadedAt < IGNORE_FIRST_MS) return;
      for (const m of mutations) {
        for (const node of m.addedNodes) {
          if (node.nodeType !== Node.ELEMENT_NODE) continue;
          // Twitch часто добавляет не само сообщение, а обёртку, внутри которой оно лежит,
          // поэтому проверяем и сам элемент, и всё, что внутри него.
          if (node.matches(messageSelector)) {
            detector.addMessage();
          } else {
            const inner = node.querySelectorAll(messageSelector).length;
            for (let i = 0; i < inner; i++) detector.addMessage();
          }
        }
      }
    });
    observer.observe(document.body, { childList: true, subtree: true });

    // Раз в 15 секунд пишем в консоль, что видит детектор, — так понятно, что счётчик работает
    // и насколько чат далёк от порога.
    setInterval(() => {
      const st = detector.stats();
      const status = st.warmingUp
        ? `набираю статистику, ещё ${st.warmUpLeftSeconds} с`
        : `порог: в ${settings.ratio} раза выше обычного и ≥ ${settings.minMessages} сообщ. за ${settings.windowSeconds} с`;
      console.log(
        `[Clipper] за ${settings.windowSeconds} с: ${st.recent} сообщ. (${st.rate.toFixed(2)}/с), обычно ${st.baseline.toFixed(2)}/с — ${status}`
      );
    }, 15000);

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
