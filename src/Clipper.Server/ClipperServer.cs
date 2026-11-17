using System.Text.Json.Serialization;
using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Clipper.Core.Planning;
using Clipper.Cutter;
using Clipper.Recorder;
using Clipper.Subtitles;
using Clipper.YouTube;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clipper.Server;

/// <summary>
/// Собирает ядро Clipper: HTTP API для расширения, рекордер и нарезчик.
/// Используется и консольным Clipper.App, и десктопным клиентом Clipper.Desktop.
/// </summary>
public static class ClipperServer
{
    /// <param name="options">Настройки. null — прочитать из appsettings.json (секция "Clipper").</param>
    /// <param name="args">Аргументы командной строки (для консольного режима).</param>
    /// <param name="configureLogging">Дополнительная настройка логов (клиент выводит их в окно).</param>
    public static WebApplication Build(
        ClipperOptions? options = null,
        string[]? args = null,
        Action<ILoggingBuilder>? configureLogging = null)
    {
        // Корень — папка с программой: там лежит appsettings.json (и не важно, откуда запустили).
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args ?? Array.Empty<string>(),
            ContentRootPath = AppContext.BaseDirectory,
        });

        options ??= builder.Configuration.GetSection(ClipperOptions.SectionName).Get<ClipperOptions>() ?? new ClipperOptions();
        builder.WebHost.UseUrls(options.ListenUrl);
        configureLogging?.Invoke(builder.Logging);

        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton<ClipPlanner>();
        builder.Services.AddSingleton<RecorderPool>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<RecorderPool>());
        builder.Services.AddSingleton<RecordingCleaner>();
        builder.Services.AddHostedService(sp => sp.GetRequiredService<RecordingCleaner>());
        builder.Services.AddSingleton<IClipCutter, FfmpegClipCutter>();
        builder.Services.AddSingleton<ITranscriber, WhisperTranscriber>();
        builder.Services.AddSingleton<YouTubeAuth>();
        builder.Services.AddSingleton<IClipPublisher, YouTubeShortsPublisher>();
        builder.Services.AddSingleton<ClipUploader>();
        builder.Services.AddHostedService<ClipWorker>();

        builder.Services.ConfigureHttpJsonOptions(o =>
            o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        // Расширение обращается к ядру со страниц twitch.tv / youtube.com — разрешаем CORS.
        builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

        var app = builder.Build();
        app.UseCors();
        MapApi(app);
        return app;
    }

    private static void MapApi(WebApplication app)
    {
        app.MapGet("/", () => Results.Redirect("/api/status"));

        // Состояние: какие стримы пишутся и какие клипы в работе.
        app.MapGet("/api/status", (RecorderPool pool, ClipPlanner planner) => new
        {
            Streams = pool.Recorders.Select(r => new { r.Stream.Key, r.Stream.Url, Recordings = r.Sessions }),
            // Для старых версий расширения: «идёт ли запись хоть одного стрима».
            Recordings = pool.Recorders.SelectMany(r => r.Sessions),
            Clips = planner.Jobs,
        });

        // Всплеск активности чата (шлёт расширение).
        app.MapPost("/api/spikes", (ChatSpike spike, RecorderPool pool, ClipPlanner planner, ILoggerFactory loggers) =>
        {
            var log = loggers.CreateLogger("Clipper.Api");
            var stream = StreamSource.Match(spike, pool.Streams);
            if (stream is null)
            {
                log.LogInformation("Всплеск {Platform}/{Channel} пропущен: этот канал не записывается", spike.Platform, spike.Channel);
                return Results.NotFound(new { error = $"Канал {spike.Channel} не записывается" });
            }

            var job = planner.AddSpike(spike, stream);
            log.LogInformation(
                "Всплеск {Stream}: {Rate:0.0} сообщ/с (обычно {Base:0.0}) → клип {Start:HH:mm:ss}–{End:HH:mm:ss}",
                stream.Key, spike.MessagesPerSecond, spike.BaselinePerSecond,
                job.Start.ToLocalTime(), job.End.ToLocalTime());
            return Results.Ok(job);
        });

        // Загрузить готовый клип в Shorts вручную.
        app.MapPost("/api/clips/{id:guid}/upload", async (Guid id, ClipPlanner planner, ClipUploader uploader, CancellationToken ct) =>
        {
            var job = planner.Jobs.FirstOrDefault(j => j.Id == id);
            if (job is null) return Results.NotFound();
            await uploader.UploadAsync(job, ct);
            return Results.Ok(job);
        });

        // Ручной клип «вот прямо сейчас было классно»: ?stream=имя — один стрим, без параметра — все.
        app.MapPost("/api/clip-now", (string? stream, RecorderPool pool, ClipPlanner planner) =>
        {
            var targets = pool.Streams
                .Where(s => stream is null || s.Key.Equals(stream, StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (targets.Count == 0) return Results.NotFound();
            return Results.Ok(targets.Select(s => planner.AddSpike(ChatSpike.Manual(), s)).ToList());
        });
    }
}
