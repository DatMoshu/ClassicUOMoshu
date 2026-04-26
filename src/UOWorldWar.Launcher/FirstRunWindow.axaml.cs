using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace UOWorldWar.Launcher;

public partial class FirstRunWindow : Window
{
    private const string EndlessJourneyUrl = "https://uo.com/client-download/";

    private readonly SettingsFile _settings;
    private readonly string _settingsPath;
    private readonly UoPathDetectionResult _initialDetection;

    private string? _selectedPath;
    private TextBlock _statusText = null!;
    private TextBlock _selectedPathText = null!;
    private Button _continueButton = null!;

    // XAML loader requires a parameterless constructor for the previewer.
    public FirstRunWindow() : this(SettingsFile.Load(""), "", new UoPathDetectionResult(UoPathStatus.NotFound, null, null)) { }

    public FirstRunWindow(SettingsFile settings, string settingsPath, UoPathDetectionResult detection)
    {
        _settings = settings;
        _settingsPath = settingsPath;
        _initialDetection = detection;
        InitializeComponent();
        Wire();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void Wire()
    {
        _statusText = this.FindControl<TextBlock>("StatusText")!;
        _selectedPathText = this.FindControl<TextBlock>("SelectedPathText")!;
        _continueButton = this.FindControl<Button>("ContinueButton")!;

        _statusText.Text = _initialDetection.Status switch
        {
            UoPathStatus.InvalidPath => $"The previously configured path is no longer valid: {_initialDetection.Path}",
            UoPathStatus.NotFound => "We checked the standard install locations and couldn't find UO.",
            _ => string.Empty,
        };

        this.FindControl<Button>("OpenEaButton")!.Click += OnOpenEaClicked;
        this.FindControl<Button>("BrowseButton")!.Click += OnBrowseClicked;
        this.FindControl<Button>("CancelButton")!.Click += OnCancelClicked;
        _continueButton.Click += OnContinueClicked;
    }

    private void OnOpenEaClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = EndlessJourneyUrl,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            _statusText.Text = "Could not open the browser. Visit https://uo.com/client-download/ manually.";
        }
    }

    private async void OnBrowseClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select your Ultima Online folder",
                AllowMultiple = false,
            });
            if (folders.Count == 0) return;

            var path = folders[0].TryGetLocalPath();
            if (string.IsNullOrEmpty(path)) return;

            if (UoPathDetector.IsValidUoDirectory(path))
            {
                _selectedPath = path;
                _selectedPathText.Text = $"Selected: {path}";
                _continueButton.IsEnabled = true;
                _statusText.Text = "Looks good — that folder has the files we need.";
            }
            else
            {
                _selectedPath = null;
                _continueButton.IsEnabled = false;
                _selectedPathText.Text = $"Selected: {path}";
                _statusText.Text = "That folder doesn't look like a UO install (no client.exe + asset files). Try the EA Endless Journey installer.";
            }
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            _statusText.Text = "Folder picker failed. Please type the path into settings.json manually.";
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        App.LauncherExitCode = 10; // user cancelled
        Close();
        ShutdownApp();
    }

    private void OnContinueClicked(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_selectedPath)) return;
        try
        {
            _settings.UltimaOnlineDirectory = _selectedPath;
            _settings.Save(_settingsPath);
            App.LauncherExitCode = Program.LaunchClient();
        }
        catch (Exception ex)
        {
            CrashLog.Write(ex);
            App.LauncherExitCode = 11;
        }
        Close();
        ShutdownApp();
    }

    private static void ShutdownApp()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
