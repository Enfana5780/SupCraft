using System.Diagnostics;
using System.Text;

namespace SupCraft.Core;

public interface IToolRunner
{
    Task RunAsync(string executable, IReadOnlyList<string> arguments, string work, IReadOnlyDictionary<string, string>? environment, Action<string> log, CancellationToken token);
}

public sealed class ToolRunner : IToolRunner
{
    public async Task RunAsync(string executable, IReadOnlyList<string> arguments, string work, IReadOnlyDictionary<string, string>? environment, Action<string> log, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var info = new ProcessStartInfo(executable) { WorkingDirectory = work, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8 };
        foreach (string argument in arguments) info.ArgumentList.Add(argument);
        if (environment != null) foreach (var pair in environment) info.Environment[pair.Key] = pair.Value;
        using var process = new Process { StartInfo = info };
        var tail = new Queue<string>();
        void Capture(string line) { log(line); lock (tail) { tail.Enqueue(line); while (tail.Count > 80) tail.Dequeue(); } }
        try
        {
            if (!process.Start()) throw new ConversionException("启动", "无法启动转换组件。", executable);
            using var registration = token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
            async Task Drain(StreamReader stream) { while (await stream.ReadLineAsync() is { } line) Capture(line); }
            var output = Drain(process.StandardOutput); var error = Drain(process.StandardError);
            // Cancellation kills the full tree; always wait and drain before releasing job files.
            await process.WaitForExitAsync(CancellationToken.None);
            await Task.WhenAll(output, error);
            token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new ConversionException("转换核心", $"{Path.GetFileName(executable)} 退出码 {process.ExitCode}。", string.Join('\n', tail));
        }
        catch (System.ComponentModel.Win32Exception ex) { throw new ConversionException("启动", "无法运行转换组件，请重新解压完整 Mac 程序包。", ex.Message); }
    }
}
