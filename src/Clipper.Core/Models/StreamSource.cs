namespace Clipper.Core.Models;

/// <summary>
/// Один стрим, который записываем. Key — короткое имя канала: по нему клипы
/// раскладываются по папкам и по нему же всплеск из чата находит свой стрим.
/// </summary>
/// <param name="Key">twitch: имя канала; youtube: @handle, id видео или id канала.</param>
/// <param name="Platform">"twitch", "youtube" или "other".</param>
/// <param name="Url">Ссылка, которую получит streamlink.</param>
public sealed record StreamSource(string Key, string Platform, string Url)
{
    /// <summary>Разбирает ссылку: https://www.twitch.tv/Fubgun → ("fubgun", "twitch").</summary>
    public static StreamSource FromUrl(string url)
    {
        url = url.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return new StreamSource(SafeName(url), "other", url);

        var host = uri.Host.ToLowerInvariant();
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (host.EndsWith("twitch.tv"))
        {
            // twitch.tv/имя или twitch.tv/popout/имя/chat
            var name = parts.Length > 0 && parts[0] == "popout" && parts.Length > 1 ? parts[1] : parts.FirstOrDefault();
            return new StreamSource(SafeName(name ?? "twitch").ToLowerInvariant(), "twitch", url);
        }

        if (host.EndsWith("youtube.com") || host == "youtu.be")
        {
            var videoId = System.Web.HttpUtility.ParseQueryString(uri.Query)["v"];
            var key = host == "youtu.be" ? parts.FirstOrDefault()
                : videoId
                ?? parts.FirstOrDefault(p => p.StartsWith('@'))
                ?? (parts.Length > 1 && parts[0] is "channel" or "c" or "user" ? parts[1] : null)
                ?? (parts.Length > 1 && parts[0] == "live" ? parts[1] : null);
            return new StreamSource(SafeName(key ?? "youtube"), "youtube", url);
        }

        return new StreamSource(SafeName(host), "other", url);
    }

    /// <summary>
    /// К какому стриму относится всплеск из чата. Сначала ищем по имени канала;
    /// если не нашли (у YouTube чат знает только id видео, а в ссылке может быть @handle) —
    /// берём единственный стрим этой платформы. Ручной клип без канала — первый стрим.
    /// </summary>
    public static StreamSource? Match(ChatSpike spike, IReadOnlyList<StreamSource> streams)
    {
        var exact = streams.FirstOrDefault(s => s.Key.Equals(spike.Channel, StringComparison.OrdinalIgnoreCase));
        if (exact is not null) return exact;

        var samePlatform = streams.Where(s => s.Platform == spike.Platform).ToList();
        if (samePlatform.Count == 1) return samePlatform[0];

        return spike.Platform == "manual" && streams.Count > 0 ? streams[0] : null;
    }

    /// <summary>Имя, безопасное для папки на Windows и macOS.</summary>
    private static string SafeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().Concat(new[] { ':', '?', '*', '"', '<', '>', '|' }).ToHashSet();
        var safe = new string(name.Select(c => invalid.Contains(c) ? '_' : c).ToArray()).Trim('.', ' ');
        return safe.Length == 0 ? "stream" : safe;
    }
}
