// Twitch: чат прямо на странице канала (www.twitch.tv/имя) или в popout-окне
// (www.twitch.tv/popout/имя/chat). Если Twitch поменяет вёрстку — править селекторы здесь.
startChatWatcher({
  platform: "twitch",
  getChannel: () => {
    const parts = location.pathname.split("/").filter(Boolean);
    return parts[0] === "popout" ? parts[1] : parts[0];
  },
  messageSelector: '.chat-line__message, [data-a-target="chat-line-message"]',
  // Плашки подписок, подарков и рейдов.
  eventSelector: ".user-notice-line, [data-test-selector='user-notice-line']",
  getText: textWithEmotes,
  classify: (el) => {
    if (el.matches(".user-notice-line, [data-test-selector='user-notice-line']")) {
      const text = el.textContent.toLowerCase();
      return /raid|рейд|raider/.test(text) ? "raid" : "sub";
    }
    // Битсы: эмоут-«чирмоут» вида Cheer100 внутри сообщения.
    const cheer = [...el.querySelectorAll("img")].some((img) => /^[a-z]*cheer\d+$/i.test(img.alt || ""));
    return cheer ? "donation" : null;
  },
});
