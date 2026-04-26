using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace UOWorldWar.Launcher;

/// <summary>
/// Read/write wrapper around settings.json. We treat the file as a generic
/// JSON object so we never lose unknown fields written by the client itself.
/// Only the fields the launcher needs are surfaced as typed properties.
/// </summary>
public sealed class SettingsFile
{
    private readonly JsonObject _root;

    private SettingsFile(JsonObject root) => _root = root;

    public string UltimaOnlineDirectory
    {
        get => _root["ultimaonlinedirectory"]?.GetValue<string>() ?? string.Empty;
        set => _root["ultimaonlinedirectory"] = value;
    }

    public static SettingsFile Load(string path)
    {
        if (!File.Exists(path))
        {
            return new SettingsFile(new JsonObject());
        }
        var text = File.ReadAllText(path);
        var node = JsonNode.Parse(text) as JsonObject ?? new JsonObject();
        return new SettingsFile(node);
    }

    public void Save(string path)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(path, _root.ToJsonString(options));
    }
}
