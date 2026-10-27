using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhaleCompanion;

public static class AccessorySelection
{
    public const int PotGroup = 55000;
    public static bool TryEvaluate(int group, bool enabled, out bool visible)
    { visible = enabled; return group == PotGroup; }
}

public sealed record AppearanceSettings
{
    public bool ShowHeadPot { get; init; } = false;
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true };
    public static AppearanceSettings Load(string path) => File.Exists(path)
        ? JsonSerializer.Deserialize<AppearanceSettings>(File.ReadAllText(path), Options) ?? throw new InvalidDataException("Invalid appearance settings") : new();
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, Options));
        File.Move(temp, path, true);
    }
}
