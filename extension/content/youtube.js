// YouTube: чат трансляции живёт в отдельном iframe www.youtube.com/live_chat?v=ID_ВИДЕО,
// поэтому скрипт запускается внутри него (all_frames в manifest.json).
startChatWatcher({
  platform: "youtube",
  getChannel: () => new URLSearchParams(location.search).get("v") || "unknown",
  messageSelector: "yt-live-chat-text-message-renderer",
  // Суперчаты, суперстикеры и спонсорство.
  eventSelector:
    "yt-live-chat-paid-message-renderer, yt-live-chat-paid-sticker-renderer, yt-live-chat-membership-item-renderer",
  getText: (el) => textWithEmotes(el.querySelector("#message") || el),
  classify: (el) => {
    const tag = el.tagName.toLowerCase();
    if (tag === "yt-live-chat-paid-message-renderer" || tag === "yt-live-chat-paid-sticker-renderer") return "donation";
    if (tag === "yt-live-chat-membership-item-renderer") return "sub";
    return null;
  },
});
