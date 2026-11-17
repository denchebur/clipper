// Детектор всплесков: сравнивает активность чата за последние windowSeconds
// с обычной активностью за предыдущие baselineSeconds.
//
// Активность — это не просто число сообщений, а их «вес»:
//   обычное сообщение — 1,
//   сообщение со смехом/эмоутами (KEKW, ахах, 😂) — hypeWeight,
//   подписка — 3, донат — 5 (CLIPPER_EVENT_WEIGHTS).
// Рейд — отдельный случай: сразу считается моментом для клипа (instant).
//
// Чистая логика без DOM — её легко менять и проверять.
class SpikeDetector {
  constructor(settings) {
    this.settings = settings;
    this.items = []; // { t: время мс, w: вес, hype: было ли «реакционное» сообщение }
    this.startedAt = Date.now();
    this.lastSpikeAt = 0;
    this.pendingInstant = null;
    this.hypeTerms = SpikeDetector.parseTerms(settings.hypeWords || "");
  }

  static parseTerms(text) {
    return text
      .split(",")
      .map((t) => t.trim().toLowerCase())
      .filter((t) => t.length > 0);
  }

  // Есть ли в тексте «реакционное» слово. Короткие слова (W, GG, лол) ищем целиком,
  // длинные — как часть слова (ахах найдётся в «ахахахах»).
  isHype(text) {
    if (!text || this.hypeTerms.length === 0) return false;
    const lower = text.toLowerCase();
    const tokens = lower.split(/[\s,.:;()]+/).filter(Boolean);
    return this.hypeTerms.some((term) => (term.length >= 4 || /[^\p{L}\p{N}]/u.test(term) ? lower.includes(term) : tokens.includes(term)));
  }

  // Обычное сообщение чата (text — его текст вместе с названиями эмоутов).
  addMessage(time = Date.now(), text = "") {
    const hype = this.isHype(text);
    this.items.push({ t: time, w: hype ? this.settings.hypeWeight : 1, hype });
  }

  // Событие: "sub", "donation" или "raid".
  addEvent(type, time = Date.now()) {
    if (!this.settings.useEvents) return;
    if (type === "raid") {
      this.pendingInstant = { reason: "raid", t: time };
      return;
    }
    this.items.push({ t: time, w: CLIPPER_EVENT_WEIGHTS[type] || 1, event: type });
  }

  // Текущие цифры: сколько сообщений и какой «вес» в окне, текущая и обычная активность.
  stats(now = Date.now()) {
    const s = this.settings;
    const windowStart = now - s.windowSeconds * 1000;
    const baselineMs = s.baselineSeconds * 1000;
    const recentItems = this.items.filter((i) => i.t >= windowStart);
    const olderItems = this.items.filter((i) => i.t < windowStart && i.t >= windowStart - baselineMs);
    const observedMs = Math.min(baselineMs, windowStart - this.startedAt);
    const warmUpLeftMs = baselineMs / 2 - (now - this.startedAt);
    const sum = (arr) => arr.reduce((acc, i) => acc + i.w, 0);
    return {
      recent: recentItems.filter((i) => !i.event).length,
      hype: recentItems.filter((i) => i.hype).length,
      events: recentItems.filter((i) => i.event).length,
      rate: sum(recentItems) / s.windowSeconds,
      baseline: observedMs > 0 ? sum(olderItems) / (observedMs / 1000) : 0,
      warmingUp: warmUpLeftMs > 0,
      warmUpLeftSeconds: Math.max(0, Math.ceil(warmUpLeftMs / 1000)),
    };
  }

  // Вызывается раз в секунду. Возвращает объект всплеска или null.
  check(now = Date.now()) {
    const s = this.settings;
    const baselineMs = s.baselineSeconds * 1000;

    // Выкидываем старые записи.
    const oldest = now - s.windowSeconds * 1000 - baselineMs;
    while (this.items.length && this.items[0].t < oldest) this.items.shift();

    const inCooldown = now - this.lastSpikeAt < s.cooldownSeconds * 1000;

    // Рейд — сразу момент для клипа (даже во время разогрева).
    if (this.pendingInstant) {
      const instant = this.pendingInstant;
      this.pendingInstant = null;
      if (!inCooldown) {
        this.lastSpikeAt = now;
        return { messagesPerSecond: 0, baselinePerSecond: 0, reason: instant.reason };
      }
    }

    // Пока не накопили хотя бы половину фонового окна, не знаем, что для чата «нормально».
    if (now - this.startedAt < baselineMs / 2) return null;
    if (inCooldown) return null;

    const st = this.stats(now);
    // Минимум 0.05 в секунду, чтобы в почти пустом чате не делить на ноль.
    const baseline = Math.max(st.baseline, 0.05);

    if (st.recent + st.events >= s.minMessages && st.rate >= baseline * s.ratio) {
      this.lastSpikeAt = now;
      // Почему сработало: больше половины сообщений с эмоутами/смехом — «emotes», есть донаты/подписки — «events».
      const reason = st.events > 0 ? "events" : st.hype * 2 >= st.recent ? "emotes" : "chat";
      return { messagesPerSecond: st.rate, baselinePerSecond: baseline, reason };
    }
    return null;
  }
}
