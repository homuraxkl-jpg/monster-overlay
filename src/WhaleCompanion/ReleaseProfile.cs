using System.Text.Json;
namespace WhaleCompanion;
public static class ReleaseProfile
{
    public static bool ResearchExportsEnabled=>false;
    public static bool ModelOutlineEnabled=>false;
}
public sealed record LogSettings
{
    public bool Detailed {get;init;}=false;
    public int MaxBytes {get;init;}=1048576;
    public static LogSettings Load(string path)
    {
        if(!File.Exists(path))return new();
        if(new FileInfo(path).Length>4096)throw new InvalidDataException();
        var value=JsonSerializer.Deserialize<LogSettings>(File.ReadAllText(path))??throw new InvalidDataException();
        if(value.MaxBytes is <65536 or >8388608)throw new InvalidDataException();return value;
    }
    public static bool Keep(string text,bool detailed)=>detailed||text.StartsWith("P11.",StringComparison.Ordinal)||text.StartsWith("FACE_EXECUTOR_READY",StringComparison.Ordinal)||text.StartsWith("EVENT_",StringComparison.Ordinal)||text.StartsWith("SHARED_CHAT_",StringComparison.Ordinal)||text.StartsWith("NATIVE_DISPLAY",StringComparison.Ordinal)||text.StartsWith("NATIVE_UTF8_READY",StringComparison.Ordinal)||new[]{"FAILED","ERROR","STOPPED","mismatch","invalid"}.Any(word=>text.Contains(word,StringComparison.OrdinalIgnoreCase));
}
