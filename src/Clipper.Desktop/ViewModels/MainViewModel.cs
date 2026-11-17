using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Clipper.Core;
using Clipper.Core.Abstractions;
using Clipper.Core.Models;
using Clipper.Core.Planning;
using Clipper.Desktop.Services;
using Clipper.Server;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Clipper.Desktop.ViewModels;

/// <summary>
/// Логика главного окна: настройки, запуск/остановка ядра, список клипов и лог.
/// Ядро (то же, что в Clipper.App) запускается прямо внутри клиента.
/// </summary>
public partial class MainViewModel : ObservableObject
{
    private const int MaxLogLines = 500;

    private WebApplication? _server;
    private readonly DispatcherTimer _refreshTimer;

    public MainViewModel()
    {
        Options = SettingsStore.Load();
        _refreshTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh());
    }

    /// <summary>Настройки, к которым привязана форма. Сохраняются в settings.json.</summary>
    public ClipperOptions Options { get; }

    public string[] Qualities { get; } = { "best", "1080p60", "1080p", "720p60", "720p", "480p" };
    public string[] VerticalModes { get; } = { "blur", "crop" };
    public string[] WhisperModels { get; } = { "tiny", "base", "small", "medium", "largev3turbo" };

    public ObservableCollection<ClipRow> Clips { get; } = new();
    public ObservableCollection<string> Log { get; } = new();

    public string SettingsPath => SettingsStore.FilePath;

    /// <summary>Нужен, чтобы открывать файлы и папки системным способом. Задаётся окном.</summary>
    public ILauncher? Launcher { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartStopText))]
    [NotifyCanExecuteChangedFor(nameof(ClipNowCommand))]
    private bool _isRunning;

    [ObservableProperty] private string _statusText = "Остановлено. Вставьте ссылку на стрим и нажмите «Старт».";

    public string StartStopText => IsRunning ? "■ Стоп" : "▶ Старт";

    [RelayCommand]
    private async Task StartStopAsync()
    {
        if (IsRunning) await StopAsync();
        else await StartAsync();
    }

    private async Task StartAsync()
    {
        if (string.IsNullOrWhiteSpace(Options.StreamUrl))
        {
            StatusText = "Сначала вставьте ссылку на стрим.";
            return;
        }

        Options.StreamUrl = Options.StreamUrl.Trim();
        SaveSettings();

        try
        {
            _server = ClipperServer.Build(SettingsStore.Clone(Options), configureLogging: logging =>
            {
                logging.ClearProviders();
                logging.AddFilter("Microsoft", LogLevel.Warning);
                logging.AddProvider(new UiLoggerProvider(AddLog));
            });
            await _server.StartAsync();
        }
        catch (Exception ex)
        {
            AddLog($"Не удалось запустить ядро: {ex.Message}");
            StatusText = $"Ошибка запуска: {ex.Message} (может, уже запущен Clipper.App на том же порту?)";
            await DisposeServerAsync();
            return;
        }

        Clips.Clear();
        IsRunning = true;
        StatusText = "Запущено, ждём начала записи…";
        _refreshTimer.Start();
    }

    private async Task StopAsync()
    {
        _refreshTimer.Stop();
        StatusText = "Останавливаю…";
        await DisposeServerAsync();
        IsRunning = false;
        StatusText = "Остановлено.";
    }

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void ClipNow()
    {
        _server?.Services.GetRequiredService<ClipPlanner>().AddSpike(ChatSpike.Manual());
        Refresh();
    }

    [RelayCommand]
    private void SaveSettings()
    {
        SettingsStore.Save(Options);
        if (IsRunning) StatusText = "Настройки сохранены. Перезапустите (Стоп → Старт), чтобы они применились.";
    }

    [RelayCommand]
    private async Task OpenClipsFolderAsync()
    {
        Directory.CreateDirectory(Options.ClipsDir);
        if (Launcher is not null)
            await Launcher.LaunchDirectoryInfoAsync(new DirectoryInfo(Options.ClipsDir));
    }

    [RelayCommand]
    private async Task OpenClipAsync(ClipRow? row)
    {
        if (row?.OutputPath is { } path && File.Exists(path) && Launcher is not null)
            await Launcher.LaunchFileInfoAsync(new FileInfo(path));
    }

    [RelayCommand]
    private void ClearLog() => Log.Clear();

    /// <summary>Вызывается окном при закрытии.</summary>
    public async Task ShutdownAsync()
    {
        SettingsStore.Save(Options);
        if (IsRunning) await StopAsync();
    }

    /// <summary>Раз в секунду обновляем статус записи и список клипов.</summary>
    private void Refresh()
    {
        if (_server is null) return;

        var recorder = _server.Services.GetRequiredService<IStreamRecorder>();
        var active = recorder.Sessions.LastOrDefault(s => s.IsActive);
        StatusText = active is null
            ? "Запущено, но запись не идёт: стрим офлайн или streamlink не нашёл поток (см. «Лог»)."
            : $"● Идёт запись с {active.StartedAt.ToLocalTime():HH:mm:ss}: {Path.GetFileName(active.FilePath)}";

        var jobs = _server.Services.GetRequiredService<ClipPlanner>().Jobs; // новые сверху
        foreach (var job in jobs.Reverse())
        {
            var row = Clips.FirstOrDefault(r => r.Id == job.Id);
            if (row is null) Clips.Insert(0, new ClipRow(job));
            else row.Update(job);
        }
    }

    private void AddLog(string line) => Dispatcher.UIThread.Post(() =>
    {
        Log.Add(line);
        while (Log.Count > MaxLogLines) Log.RemoveAt(0);
    });

    private async Task DisposeServerAsync()
    {
        if (_server is null) return;
        try { await _server.StopAsync(); } catch { /* уже остановлен */ }
        await _server.DisposeAsync();
        _server = null;
    }
}
