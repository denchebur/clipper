using System.Diagnostics;
using System.Text;

namespace Clipper.Core.Infrastructure;

/// <summary>
/// Запуск внешних программ (ffmpeg, streamlink). Аргументы передаются списком,
/// поэтому пробелы в путях не ломают команду.
/// </summary>
public static class ExternalProcess
{
    public sealed record Result(int ExitCode, string StdErrTail);

    public static async Task<Result> RunAsync(
        string fileName,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken,
        Action<string>? onStdErrLine = null)
    {
        var psi = new ProcessStartInfo(ResolveExecutable(fileName))
        {
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (var arg in arguments)
            psi.ArgumentList.Add(arg);

        using var process = new Process { StartInfo = psi };

        // Храним только последние строки stderr — этого хватает, чтобы понять ошибку.
        var tail = new Queue<string>();
        void OnLine(string? line)
        {
            if (line is null) return;
            onStdErrLine?.Invoke(line);
            lock (tail)
            {
                tail.Enqueue(line);
                while (tail.Count > 20) tail.Dequeue();
            }
        }

        process.ErrorDataReceived += (_, e) => OnLine(e.Data);
        process.OutputDataReceived += (_, e) => OnLine(e.Data);

        try
        {
            process.Start();
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"Не удалось запустить '{fileName}'. Установлена ли программа и есть ли она в PATH? ({ex.Message})", ex);
        }

        process.BeginErrorReadLine();
        process.BeginOutputReadLine();

        try
        {
            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { /* уже завершился */ }
            throw;
        }

        string text;
        lock (tail) text = string.Join(Environment.NewLine, tail);
        return new Result(process.ExitCode, text);
    }

    /// <summary>
    /// Находит программу по имени ("ffmpeg") в PATH и в типичных папках Homebrew.
    /// На macOS приложение, запущенное из Finder, не видит PATH терминала,
    /// поэтому /opt/homebrew/bin и /usr/local/bin проверяем сами.
    /// Если передан полный путь или ничего не нашлось, возвращает имя как есть.
    /// </summary>
    public static string ResolveExecutable(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || Path.IsPathRooted(name) || name.Contains(Path.DirectorySeparatorChar))
            return name;

        var dirs = (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Concat(new[] { "/opt/homebrew/bin", "/usr/local/bin", "/usr/bin" });

        var candidates = OperatingSystem.IsWindows() && !name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? new[] { name + ".exe", name }
            : new[] { name };

        foreach (var dir in dirs)
        foreach (var candidate in candidates)
        {
            var full = Path.Combine(dir, candidate);
            if (File.Exists(full)) return full;
        }
        return name;
    }
}
