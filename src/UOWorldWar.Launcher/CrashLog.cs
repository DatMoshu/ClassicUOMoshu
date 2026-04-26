using System;
using System.IO;

namespace UOWorldWar.Launcher;

internal static class CrashLog
{
    public static void Write(Exception ex)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "Logs");
            Directory.CreateDirectory(dir);
            var file = Path.Combine(dir, $"launcher-crash-{DateTime.Now:yyyyMMdd-HHmmss}.txt");
            File.WriteAllText(file, ex.ToString());
        }
        catch
        {
            // Crash inside the crash logger is not actionable — swallow.
        }
    }
}
