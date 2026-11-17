// Twitch: чат прямо на странице канала (www.twitch.tv/имя) или в popout-окне
// (www.twitch.tv/popout/имя/chat). Если Twitch поменяет вёрстку — править селектор здесь.
startChatWatcher({
  platform: "twitch",
  getChannel: () => {
    const parts = location.pathname.split("/").filter(Boolean);
    return parts[0] === "popout" ? parts[1] : parts[0];
  },
  messageSelector: '.chat-line__message, [data-a-target="chat-line-message"]',
});
