namespace Clipper.Core.Models;

/// <summary>
/// Всплеск активности в чате. Приходит от браузерного расширения
/// (или создаётся вручную кнопкой «Клип сейчас»).
/// </summary>
/// <param name="Timestamp">Момент всплеска (время на компьютере зрителя).</param>
/// <param name="Platform">"twitch", "youtube" или "manual".</param>
/// <param name="Channel">Канал Twitch или id видео YouTube.</param>
/// <param name="MessagesPerSecond">Скорость сообщений в момент всплеска.</param>
/// <param name="BaselinePerSecond">Обычная (фоновая) скорость сообщений.</param>
public sealed record ChatSpike(
    DateTimeOffset Timestamp,
    string Platform,
    string Channel,
    double MessagesPerSecond = 0,
    double BaselinePerSecond = 0)
{
    /// <summary>Во сколько раз активность выше обычной.</summary>
    public double Ratio => BaselinePerSecond > 0 ? MessagesPerSecond / BaselinePerSecond : 0;

    /// <summary>Ручной клип на текущий момент (кнопка «Клип сейчас»).</summary>
    public static ChatSpike Manual() => new(DateTimeOffset.UtcNow, "manual", "manual");
}
