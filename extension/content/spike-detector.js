// Детектор всплесков: сравнивает скорость сообщений за последние windowSeconds
// с обычной скоростью за предыдущие baselineSeconds.
// Чистая логика без DOM — её легко менять и проверять.
class SpikeDetector {
  constructor(settings) {
    this.settings = settings;
    this.timestamps = []; // времена сообщений, мс
    this.startedAt = Date.now();
    this.lastSpikeAt = 0;
  }

  addMessage(time = Date.now()) {
    this.timestamps.push(time);
  }

  // Текущие цифры для отладки: сколько сообщений в окне, текущая и обычная скорость.
  stats(now = Date.now()) {
    const s = this.settings;
    const windowStart = now - s.windowSeconds * 1000;
    const baselineMs = s.baselineSeconds * 1000;
    const recent = this.timestamps.filter((t) => t >= windowStart).length;
    const older = this.timestamps.filter((t) => t < windowStart && t >= windowStart - baselineMs).length;
    const observedMs = Math.min(baselineMs, windowStart - this.startedAt);
    const warmUpLeftMs = baselineMs / 2 - (now - this.startedAt);
    return {
      recent,
      rate: recent / s.windowSeconds,
      baseline: observedMs > 0 ? older / (observedMs / 1000) : 0,
      warmingUp: warmUpLeftMs > 0,
      warmUpLeftSeconds: Math.max(0, Math.ceil(warmUpLeftMs / 1000)),
    };
  }

  // Вызывается раз в секунду. Возвращает объект всплеска или null.
  check(now = Date.now()) {
    const s = this.settings;
    const windowMs = s.windowSeconds * 1000;
    const baselineMs = s.baselineSeconds * 1000;

    // Выкидываем старые сообщения.
    const oldest = now - windowMs - baselineMs;
    while (this.timestamps.length && this.timestamps[0] < oldest) this.timestamps.shift();

    // Пока не накопили хотя бы половину фонового окна, не знаем, что для чата «нормально».
    if (now - this.startedAt < baselineMs / 2) return null;
    if (now - this.lastSpikeAt < s.cooldownSeconds * 1000) return null;

    const windowStart = now - windowMs;
    const recent = this.timestamps.filter((t) => t >= windowStart).length;
    const older = this.timestamps.length - recent;

    const observedBaselineMs = Math.min(baselineMs, windowStart - this.startedAt);
    const rate = recent / s.windowSeconds;
    // Минимум 0.05 сообщ/с, чтобы в почти пустом чате не делить на ноль.
    const baseline = Math.max(older / (observedBaselineMs / 1000), 0.05);

    if (recent >= s.minMessages && rate >= baseline * s.ratio) {
      this.lastSpikeAt = now;
      return { messagesPerSecond: rate, baselinePerSecond: baseline };
    }
    return null;
  }
}
