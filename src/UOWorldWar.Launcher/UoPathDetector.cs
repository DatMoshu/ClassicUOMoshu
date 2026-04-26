using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace UOWorldWar.Launcher;

public enum UoPathStatus
{
    Valid,        // Path exists and contains the asset files we need
    NotFound,     // No UO install detected
    InvalidPath,  // Path was set but the directory is gone or missing files
}

public sealed record UoPathDetectionResult(UoPathStatus Status, string? Path, string? Reason);

/// <summary>
/// Locates a legitimate Ultima Online client installation. ClassicUO requires
/// EA's MUL/UOP asset files; we cannot redistribute them, so the player must
/// own UO (Endless Journey is free). This detector probes the well-known
/// install paths and the Origin/EA registry keys before falling back to UI.
/// </summary>
public static class UoPathDetector
{
    // Files the client absolutely needs to start. If any are missing, the
    // path is treated as invalid even if the directory exists.
    private static readonly string[] RequiredAssets =
    {
        "client.exe",        // EA UO client — its presence is the strongest signal
        "art.mul",           // Old format
        // We do NOT require both art.mul and artLegacyMUL.uop because UO
        // installs ship one or the other depending on era; presence of
        // client.exe + at least one of these is sufficient.
    };

    private static readonly string[] AlternateAssetSets =
    {
        "artLegacyMUL.uop",
        "tileart.uop",
    };

    public static UoPathDetectionResult Detect(string? configuredPath)
    {
        // 1. If settings.json already has a valid path, trust it.
        if (!string.IsNullOrWhiteSpace(configuredPath) && IsValidUoDirectory(configuredPath))
        {
            return new UoPathDetectionResult(UoPathStatus.Valid, configuredPath, null);
        }

        // 2. Probe registry (Windows only) and standard install dirs.
        foreach (var candidate in EnumerateCandidates())
        {
            if (!string.IsNullOrWhiteSpace(candidate) && IsValidUoDirectory(candidate))
            {
                return new UoPathDetectionResult(UoPathStatus.Valid, candidate, null);
            }
        }

        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return new UoPathDetectionResult(UoPathStatus.InvalidPath, configuredPath,
                "The configured UO directory does not contain the expected files.");
        }

        return new UoPathDetectionResult(UoPathStatus.NotFound, null,
            "No Ultima Online installation was found.");
    }

    public static bool IsValidUoDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        var hasClient = File.Exists(Path.Combine(path, "client.exe"));
        if (!hasClient)
        {
            return false;
        }

        foreach (var asset in RequiredAssets)
        {
            if (asset == "client.exe") continue; // already checked
            if (File.Exists(Path.Combine(path, asset))) return true;
        }
        foreach (var asset in AlternateAssetSets)
        {
            if (File.Exists(Path.Combine(path, asset))) return true;
        }
        return false;
    }

    private static System.Collections.Generic.IEnumerable<string?> EnumerateCandidates()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var p in EnumerateWindowsCandidates()) yield return p;
        }
        else
        {
            // Linux/Mac users running Wine — common Wine prefix locations.
            var home = Environment.GetEnvironmentVariable("HOME") ?? "";
            yield return Path.Combine(home, ".wine/drive_c/Program Files (x86)/Electronic Arts/Ultima Online Classic");
            yield return Path.Combine(home, ".wine/drive_c/Program Files/Electronic Arts/Ultima Online Classic");
        }
    }

    [SupportedOSPlatform("windows")]
    private static System.Collections.Generic.IEnumerable<string?> EnumerateWindowsCandidates()
    {
        // Registry: Origin Worlds Online (legacy) and Electronic Arts.
        foreach (var (hive, subkey, valueName) in new[]
        {
            (Microsoft.Win32.RegistryHive.LocalMachine, @"SOFTWARE\Origin Worlds Online\Ultima Online\1.0", "InstallDir"),
            (Microsoft.Win32.RegistryHive.LocalMachine, @"SOFTWARE\Origin Worlds Online\Ultima Online Third Dawn\1.0", "InstallDir"),
            (Microsoft.Win32.RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Origin Worlds Online\Ultima Online\1.0", "InstallDir"),
            (Microsoft.Win32.RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Origin Worlds Online\Ultima Online Third Dawn\1.0", "InstallDir"),
            (Microsoft.Win32.RegistryHive.LocalMachine, @"SOFTWARE\Electronic Arts\EA Games\Ultima Online Classic", "Install Dir"),
            (Microsoft.Win32.RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Ultima Online Classic", "Install Dir"),
            (Microsoft.Win32.RegistryHive.LocalMachine, @"SOFTWARE\Electronic Arts\EA Games\Ultima Online Stygian Abyss Classic", "Install Dir"),
            (Microsoft.Win32.RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Electronic Arts\EA Games\Ultima Online Stygian Abyss Classic", "Install Dir"),
        })
        {
            string? value = null;
            try
            {
                using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Default);
                using var sub = baseKey.OpenSubKey(subkey);
                value = sub?.GetValue(valueName) as string;
            }
            catch
            {
                // Best-effort — registry permissions or missing keys are normal.
            }
            yield return value;
        }

        // Filesystem fallbacks — typical install locations, both 32-bit and 64-bit Program Files.
        foreach (var pf in new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            @"C:\Program Files (x86)",
            @"C:\Program Files",
        })
        {
            if (string.IsNullOrEmpty(pf)) continue;
            yield return Path.Combine(pf, "Electronic Arts", "Ultima Online Classic");
            yield return Path.Combine(pf, "Electronic Arts", "Ultima Online Stygian Abyss Classic");
            yield return Path.Combine(pf, "EA Games", "Ultima Online Classic");
            yield return Path.Combine(pf, "Origin Games", "Ultima Online Classic");
            yield return Path.Combine(pf, "Ultima Online Classic");
        }
    }
}
