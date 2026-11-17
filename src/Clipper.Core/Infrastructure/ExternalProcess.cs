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
        var psi = new ProcessStartInfo(fileName)
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
}
