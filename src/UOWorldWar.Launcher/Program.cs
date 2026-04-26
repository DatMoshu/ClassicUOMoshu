using System;
using System.Diagnostics;
using System.IO;
using Avalonia;

namespace UOWorldWar.Launcher;

internal static class Program
{
    // Production layout: installer puts the Bootstrap (ClassicUO.exe + cuo.dll +
    // settings.json + Vivox + voice models) in {app}\, and the .NET 8
    // self-contained Launcher in {app}\Launcher\. The two cannot share a
    // directory because their runtime DLLs (System.Runtime.CompilerServices.Unsafe,
    // System.Memory, etc.) collide between .NET Framework 4.7.2 and .NET 8.
    //
    // For dev runs (`dotnet run` from src/UOWorldWar.Launcher), there is no
    // sibling ClassicUO.exe, so we fall back to AppContext.BaseDirectory itself.
    public static string InstallDir { get; private set; } = ResolveInstallDir();
    public static string SettingsPath => Path.Combine(InstallDir, "settings.json");

    private static string ResolveInstallDir()
    {
        var here = AppContext.BaseDirectory;
        var parent = Directory.GetParent(here.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (parent != null && File.Exists(Path.Combine(parent.FullName, "ClassicUO.exe")))
        {
            return parent.FullName;
        }
        return here;
    }

    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            var settings = SettingsFile.Load(SettingsPath);
            var detected = UoPathDetector.Detect(settings.UltimaOnlineDirectory);

            if (detected.Status == UoPathStatus.Valid)
            {
                if (!string.Equals(settings.UltimaOnlineDirectory, detected.Path, StringComparison.OrdinalIgnoreCase))
                {
                    settings.UltimaOnlineDirectory = detected.Path!;
                    settings.Save(SettingsPath);
                }
                return LaunchClient();
            }

            // Need user input — bring up the Avalonia wizard.
            App.PendingSettings = settings;
            App.PendingDetection = detected;
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
            return App.LauncherExitCode;
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            return 1;
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    public static int LaunchClient()
    {
        var clientExe = Path.Combine(InstallDir, "ClassicUO.exe");
        if (!File.Exists(clientExe))
        {
            CrashLog.Write(new FileNotFoundException(
                $"Bootstrap not found: {clientExe}. The installer is incomplete; please reinstall."));
            return 2;
        }

        var psi = new ProcessStartInfo
        {
            FileName = clientExe,
            WorkingDirectory = InstallDir,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi);
        return proc is null ? 3 : 0;
    }
}
