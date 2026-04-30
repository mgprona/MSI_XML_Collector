using System.Globalization;
using System.Text;

namespace Scout.Logging;

public sealed class RunLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    private RunLogger(string logPath)
    {
        LogPath = logPath;
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        _writer = new StreamWriter(logPath, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true))
        {
            AutoFlush = true,
        };
    }

    public string LogPath { get; }

    public static RunLogger Create(string baseDir)
    {
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var logPath = Path.Combine(baseDir, "Logs", $"Scout_{stamp}.log");
        return new RunLogger(logPath);
    }

    public void Info(string message) => Write(Console.Out, message);

    public void Warn(string message) => Write(Console.Error, message);

    public void Error(string message) => Write(Console.Error, message);

    private void Write(TextWriter console, string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var line = $"{timestamp} {message}";
        lock (_lock)
        {
            console.WriteLine(message);
            _writer.WriteLine(line);
        }
    }

    public void Dispose() => _writer.Dispose();
}
