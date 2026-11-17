// YouTube: чат трансляции живёт в отдельном iframe www.youtube.com/live_chat?v=ID_ВИДЕО,
// поэтому скрипт запускается внутри него (all_frames в manifest.json).
startChatWatcher({
  platform: "youtube",
  getChannel: () => new URLSearchParams(location.search).get("v") || "unknown",
  messageSelector: "yt-live-chat-text-message-renderer, yt-live-chat-paid-message-renderer",
});
