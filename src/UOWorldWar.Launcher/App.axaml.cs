using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace UOWorldWar.Launcher;

public partial class App : Application
{
    // Set by Program.Main before BuildAvaloniaApp().StartWithClassicDesktopLifetime.
    public static SettingsFile? PendingSettings { get; set; }
    public static UoPathDetectionResult? PendingDetection { get; set; }
    public static int LauncherExitCode { get; set; } = 1;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
            PendingSettings is not null && PendingDetection is not null)
        {
            desktop.MainWindow = new FirstRunWindow(PendingSettings, Program.SettingsPath, PendingDetection);
        }
        base.OnFrameworkInitializationCompleted();
    }
}
