using System.Text.Json.Serialization;
using Clipper.App;
using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Clipper.Core.Planning;
using Clipper.Cutter;
using Clipper.Recorder;

// ─── Ядро Clipper ──────────────────────────────────────────────────────────────
// Это консольное приложение с маленьким HTTP API на http://127.0.0.1:5055.
// 1. Запускает рекордер (streamlink пишет стрим в recordings/).
// 2. Принимает всплески чата от браузерного расширения: POST /api/spikes.
// 3. Фоновый воркер ждёт, пока нужный кусок запишется, и режет клип через ffmpeg в clips/.

var builder = WebApplication.CreateBuilder(args);

var options = builder.Configuration.GetSection(ClipperOptions.SectionName).Get<ClipperOptions>() ?? new ClipperOptions();
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

app.MapGet("/", () => Results.Redirect("/api/status"));

// Состояние: что пишется и какие клипы в работе.
app.MapGet("/api/status", (ClipperOptions o, IStreamRecorder recorder, ClipPlanner planner) => new
{
    o.StreamUrl,
    Recordings = recorder.Sessions,
    Clips = planner.Jobs,
});

// Всплеск активности чата (шлёт расширение).
app.MapPost("/api/spikes", (ChatSpike spike, ClipPlanner planner, ILogger<Program> log) =>
{
    var job = planner.AddSpike(spike);
    log.LogInformation("Всплеск {Platform}/{Channel}: {Rate:0.0} сообщ/с (обычно {Base:0.0}) → клип {Start:HH:mm:ss}–{End:HH:mm:ss}",
        spike.Platform, spike.Channel, spike.MessagesPerSecond, spike.BaselinePerSecond,
        job.Start.ToLocalTime(), job.End.ToLocalTime());
    return Results.Ok(job);
});

// Ручной клип «вот прямо сейчас было классно».
app.MapPost("/api/clip-now", (ClipPlanner planner) =>
    Results.Ok(planner.AddSpike(new ChatSpike(DateTimeOffset.UtcNow, "manual", "manual"))));

app.Run();
