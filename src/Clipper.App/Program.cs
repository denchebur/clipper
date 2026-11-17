using Clipper.Server;

// ─── Консольный запуск ядра Clipper ────────────────────────────────────────────
// Настройки берутся из appsettings.json (секция "Clipper").
// То же самое ядро запускает десктопный клиент Clipper.Desktop.
//
// 1. Рекордер пишет стрим через streamlink в recordings/.
// 2. Расширение присылает всплески чата: POST /api/spikes.
// 3. Фоновый воркер режет клипы через ffmpeg в clips/.

var app = ClipperServer.Build(args: args);
app.Run();
