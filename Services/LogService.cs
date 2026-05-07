using System.IO;

namespace TranslatorApp.Services;

public static class LogService
{
    private static readonly string LogPath = System.IO.Path.Combine(
        @"C:\Users\Admin\Desktop\Crawl-web\TranslatorApp", "translator.log");

    private static readonly object _lock = new();

    public static void Info(string message) => Write("INFO", message);
    public static void Error(string message, Exception? ex = null)
    {
        Write("ERROR", ex == null ? message : $"{message} | {ex.GetType().Name}: {ex.Message}");
        if (ex?.InnerException != null)
            Write("ERROR", $"  Inner: {ex.InnerException.Message}");
    }

    private static void Write(string level, string message)
    {
        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        lock (_lock)
        {
            try { File.AppendAllText(LogPath, line + Environment.NewLine); }
            catch { /* không crash app vì lỗi log */ }
        }
    }

    public static string GetLogPath() => LogPath;
}
