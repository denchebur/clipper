using System.Collections.ObjectModel;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Clipper.Core;
using Clipper.Core.Models;
using Clipper.Core.Planning;
using Clipper.Desktop.Services;
using Clipper.Server;
using Clipper.YouTube;
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
        foreach (var url in Options.Streams.DefaultIfEmpty(Options.StreamUrl))
            Streams.Add(new StreamRow { Url = url });
        _refreshTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => Refresh());
        _ = UpdateGoogleStatusAsync();
    }

    /// <summary>Настройки, к которым привязана форма. Сохраняются в settings.json.</summary>
    public ClipperOptions Options { get; }

    public string[] Qualities { get; } = { "best", "1080p60", "1080p", "720p60", "720p", "480p" };
    public string[] VerticalModes { get; } = { "blur", "crop" };
    public string[] WhisperModels { get; } = { "tiny", "base", "small", "medium", "largev3turbo" };
    public string[] PrivacyOptions { get; } = { "private", "unlisted", "public" };

    /// <summary>Стримы, которые пишем одновременно.</summary>
    public ObservableCollection<StreamRow> Streams { get; } = new();

    public ObservableCollection<ClipRow> Clips { get; } = new();
    public ObservableCollection<string> Log { get; } = new();

    public string SettingsPath => SettingsStore.FilePath;

    /// <summary>Нужен, чтобы открывать файлы и папки системным способом. Задаётся окном.</summary>
    public ILauncher? Launcher { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartStopText))]
    [NotifyCanExecuteChangedFor(nameof(ClipNowCommand))]
    private bool _isRunning;

    [ObservableProperty] private string _statusText = "Остановлено. Вставьте ссылки на стримы и нажмите «Старт».";

    public string StartStopText => IsRunning ? "■ Стоп" : "▶ Старт";

    [RelayCommand]
    private async Task StartStopAsync()
    {
        if (IsRunning) await StopAsync();
        else await StartAsync();
    }

    private async Task StartAsync()
    {
        SyncStreamsToOptions();
        if (Options.GetStreams().Count == 0)
        {
            StatusText = "Сначала вставьте хотя бы одну ссылку на стрим.";
            return;
        }
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
        foreach (var row in Streams)
        {
            row.Status = "";
            row.IsRecording = false;
        }
    }

    /// <summary>Клип сейчас — со всех записываемых стримов.</summary>
    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void ClipNow()
    {
        if (_server is null) return;
        var planner = _server.Services.GetRequiredService<ClipPlanner>();
        foreach (var stream in _server.Services.GetRequiredService<RecorderPool>().Streams)
            planner.AddSpike(ChatSpike.Manual(), stream);
        Refresh();
    }

    /// <summary>Клип сейчас — с одного стрима (кнопка ✂ в строке стрима).</summary>
    [RelayCommand]
    private void ClipStream(StreamRow? row)
    {
        if (_server is null || row is null) return;
        var key = StreamSource.FromUrl(row.Url).Key;
        var stream = _server.Services.GetRequiredService<RecorderPool>().Streams.FirstOrDefault(s => s.Key == key);
        if (stream is null) return;
        _server.Services.GetRequiredService<ClipPlanner>().AddSpike(ChatSpike.Manual(), stream);
        Refresh();
    }

    [RelayCommand]
    private void AddStream() => Streams.Add(new StreamRow());

    [RelayCommand]
    private void RemoveStream(StreamRow? row)
    {
        if (row is null) return;
        Streams.Remove(row);
        if (Streams.Count == 0) Streams.Add(new StreamRow());
    }

    private void SyncStreamsToOptions()
    {
        Options.Streams = Streams.Select(r => r.Url.Trim()).Where(u => u.Length > 0).ToList();
        Options.StreamUrl = ""; // старая настройка больше не нужна
    }

    [RelayCommand]
    private void SaveSettings()
    {
        SyncStreamsToOptions();
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

    // ─── YouTube Shorts ───

    /// <summary>Нужен, чтобы выбрать файл client_secret.json. Задаётся окном.</summary>
    public IStorageProvider? StorageProvider { get; set; }

    [ObservableProperty] private string _googleStatus = "";
    [ObservableProperty] private bool _isGoogleSignedIn;

    [RelayCommand]
    private async Task PickClientSecretsAsync()
    {
        if (StorageProvider is null) return;
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Файл ключа Google (client_secret….json)",
            FileTypeFilter = new[] { new FilePickerFileType("JSON") { Patterns = new[] { "*.json" } } },
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            Options.YouTube.ClientSecretsPath = path;
            OnPropertyChanged(nameof(Options));
            SettingsStore.Save(Options);
        }
    }

    [RelayCommand]
    private async Task GoogleSignInAsync()
    {
        try
        {
            GoogleStatus = "Откройте браузер и разрешите доступ…";
            using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
            var channel = await new YouTubeAuth(Options.YouTube).SignInAsync(cts.Token);
            IsGoogleSignedIn = true;
            GoogleStatus = $"Вход выполнен: канал «{channel}»";
            SettingsStore.Save(Options);
        }
        catch (Exception ex)
        {
            GoogleStatus = "Не удалось войти: " + ex.Message;
        }
    }

    [RelayCommand]
    private async Task GoogleSignOutAsync()
    {
        await new YouTubeAuth(Options.YouTube).SignOutAsync();
        IsGoogleSignedIn = false;
        GoogleStatus = "Вы вышли из Google.";
    }

    private async Task UpdateGoogleStatusAsync()
    {
        IsGoogleSignedIn = await new YouTubeAuth(Options.YouTube).IsSignedInAsync();
        GoogleStatus = IsGoogleSignedIn ? "Вход в Google выполнен." : "Вход в Google не выполнен.";
    }

    [RelayCommand]
    private async Task UploadClipAsync(ClipRow? row)
    {
        if (row is null || _server is null) return;
        var job = _server.Services.GetRequiredService<ClipPlanner>().Jobs.FirstOrDefault(j => j.Id == row.Id);
        if (job is null) return;
        var task = _server.Services.GetRequiredService<ClipUploader>().UploadAsync(job, CancellationToken.None);
        Refresh();
        await task;
        Refresh();
    }

    [RelayCommand]
    private async Task OpenYouTubeAsync(ClipRow? row)
    {
        if (row?.YouTubeUrl is { } url && Launcher is not null)
            await Launcher.LaunchUriAsync(new Uri(url));
    }

    /// <summary>Вызывается окном при закрытии.</summary>
    public async Task ShutdownAsync()
    {
        SyncStreamsToOptions();
        SettingsStore.Save(Options);
        if (IsRunning) await StopAsync();
    }

    /// <summary>Раз в секунду обновляем статус записи и список клипов.</summary>
    private void Refresh()
    {
        if (_server is null) return;

        var recorders = _server.Services.GetRequiredService<RecorderPool>().Recorders;
        var recording = 0;
        foreach (var row in Streams)
        {
            var key = StreamSource.FromUrl(row.Url).Key;
            var recorder = recorders.FirstOrDefault(r => r.Stream.Key == key);
            var active = recorder?.Sessions.FirstOrDefault(s => s.IsActive);
            row.IsRecording = active is not null;
            row.Status = recorder is null ? ""
                : active is null ? "ждёт стрим (офлайн?)"
                : $"● пишется с {recorder.Sessions.Where(s => s.IsActive).Min(s => s.StartedAt).ToLocalTime():HH:mm}";
            if (active is not null) recording++;
        }
        StatusText = recording == recorders.Count
            ? $"● Идёт запись: {recording} из {recorders.Count}"
            : $"Идёт запись: {recording} из {recorders.Count}. Остальные стримы офлайн или streamlink не нашёл поток (см. «Лог»).";

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
