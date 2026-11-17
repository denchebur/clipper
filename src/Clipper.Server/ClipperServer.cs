using System.Text.Json.Serialization;
using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Clipper.Core.Planning;
using Clipper.Cutter;
using Clipper.Recorder;
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
        builder.Services.AddSingleton<IStreamRecorder, StreamlinkRecorder>();
        builder.Services.AddSingleton<IClipCutter, FfmpegClipCutter>();
        builder.Services.AddHostedService<RecorderService>();
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

        // Состояние: что пишется и какие клипы в работе.
        app.MapGet("/api/status", (ClipperOptions o, IStreamRecorder recorder, ClipPlanner planner) => new
        {
            o.StreamUrl,
            Recordings = recorder.Sessions,
            Clips = planner.Jobs,
        });

        // Всплеск активности чата (шлёт расширение).
        app.MapPost("/api/spikes", (ChatSpike spike, ClipPlanner planner, ILoggerFactory loggers) =>
        {
            var job = planner.AddSpike(spike);
            loggers.CreateLogger("Clipper.Api").LogInformation(
                "Всплеск {Platform}/{Channel}: {Rate:0.0} сообщ/с (обычно {Base:0.0}) → клип {Start:HH:mm:ss}–{End:HH:mm:ss}",
                spike.Platform, spike.Channel, spike.MessagesPerSecond, spike.BaselinePerSecond,
                job.Start.ToLocalTime(), job.End.ToLocalTime());
            return Results.Ok(job);
        });

        // Ручной клип «вот прямо сейчас было классно».
        app.MapPost("/api/clip-now", (ClipPlanner planner) =>
            Results.Ok(planner.AddSpike(ChatSpike.Manual())));
    }
}
