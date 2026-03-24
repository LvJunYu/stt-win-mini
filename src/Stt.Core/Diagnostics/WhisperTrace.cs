using System.Text;

namespace Stt.Core.Diagnostics;

public static class WhisperTrace
{
    private const long MaxLogSizeBytes = 256 * 1024;
    private static readonly object SyncRoot = new();
    private static readonly string LogDirectoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "whisper",
        "logs");
    private static readonly string LogPathValue = Path.Combine(LogDirectoryPath, "whisper-trace.log");

    public static string LogPath => LogPathValue;

    public static void Log(string area, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectoryPath);

            lock (SyncRoot)
            {
                RotateIfNeeded();

                var line = new StringBuilder(128)
                    .Append(DateTimeOffset.Now.ToString("O"))
                    .Append(" [")
                    .Append(area)
                    .Append("] ")
                    .AppendLine(message)
                    .ToString();

                File.AppendAllText(LogPathValue, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Diagnostics should never break the app.
        }
    }

    private static void RotateIfNeeded()
    {
        if (!File.Exists(LogPathValue))
        {
            return;
        }

        var fileInfo = new FileInfo(LogPathValue);
        if (fileInfo.Length < MaxLogSizeBytes)
        {
            return;
        }

        File.WriteAllText(
            LogPathValue,
            $"{DateTimeOffset.Now:O} [Trace] Log rotated.{Environment.NewLine}",
            Encoding.UTF8);
    }
}
