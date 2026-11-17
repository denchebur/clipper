// Общая часть для Twitch и YouTube: следит за появлением новых сообщений и событий в DOM,
// кормит ими детектор и при всплеске отправляет событие в background.js.
//
// platform        — "twitch" / "youtube"
// getChannel()    — имя канала или id видео
// messageSelector — CSS-селектор одного сообщения чата
// getText(node)   — текст сообщения вместе с названиями эмоутов
// classify(node)  — тип события для элемента: "sub" / "donation" / "raid" / null (обычное сообщение)
// eventSelector   — CSS-селектор событий (подписки, донаты, рейды)
function startChatWatcher({ platform, getChannel, messageSelector, eventSelector, getText, classify }) {
  // Первые секунды чат подгружает историю — эти сообщения не считаем.
  const IGNORE_FIRST_MS = 5000;
  const loadedAt = Date.now();
  const anySelector = eventSelector ? `${messageSelector}, ${eventSelector}` : messageSelector;

  chrome.storage.sync.get(CLIPPER_DEFAULTS, (settings) => {
    const detector = new SpikeDetector(settings);
    // Статистику считаем с момента, когда начали засчитывать сообщения, иначе пропущенные
    // первые секунды выглядят как «тихий чат» и сразу дают ложный всплеск.
    detector.startedAt = loadedAt + IGNORE_FIRST_MS;

    const handle = (el) => {
      const type = classify ? classify(el) : null;
      if (type) detector.addEvent(type);
      else if (el.matches(messageSelector)) detector.addMessage(Date.now(), getText ? getText(el) : el.textContent);
    };

    const observer = new MutationObserver((mutations) => {
      if (Date.now() - loadedAt < IGNORE_FIRST_MS) return;
      for (const m of mutations) {
        for (const node of m.addedNodes) {
          if (node.nodeType !== Node.ELEMENT_NODE) continue;
          // Часто добавляется не само сообщение, а обёртка, внутри которой оно лежит,
          // поэтому проверяем и сам элемент, и всё, что внутри него.
          if (node.matches(anySelector)) handle(node);
          else node.querySelectorAll(anySelector).forEach(handle);
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
        `[Clipper] за ${settings.windowSeconds} с: ${st.recent} сообщ. (${st.hype} с эмоутами/смехом, событий: ${st.events}), ` +
          `активность ${st.rate.toFixed(2)}/с, обычно ${st.baseline.toFixed(2)}/с — ${status}`
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

// Текст элемента + alt у картинок (так эмоуты Twitch и эмодзи YouTube превращаются в слова: "KEKW", "😂").
function textWithEmotes(el) {
  let text = "";
  const walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT | NodeFilter.SHOW_ELEMENT);
  for (let node = walker.currentNode; node; node = walker.nextNode()) {
    if (node.nodeType === Node.TEXT_NODE) text += node.textContent;
    else if (node.tagName === "IMG" && node.alt) text += ` ${node.alt} `;
  }
  return text;
}
