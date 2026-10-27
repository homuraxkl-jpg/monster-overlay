using System.Text.Json;
using System.Text.Json.Serialization;

namespace WhaleCompanion;

public static class AccessorySelection
{
    public const int PotGroup = 55000;
    public static bool TryEvaluate(int group, bool enabled, out bool visible)
    { visible = enabled; return group == PotGroup; }
}

public static class OutlineSelection
{
    public const string ModelHash="A252AB7B8BD2DF50EAD527658ECBCEF0D521602B134E0D88BA77CF7AC2CEDB26";
    public static bool TryEvaluate(int group,bool enabled,bool pot,int face,out bool visible)
    {
        visible=enabled && (group==57000 || group==57001 && pot || group>=57010 && group<=57050 && face==56000+group-57010);
        return group is 57000 or 57001 || group>=57010 && group<=57050;
    }
    public static string Tint(string hash)=>hash switch
    {
        "092397F681CAE2D339C62D4A4543C8251422600E54AE1BFE1AD5974E289355FD"=>"Original",
        "A806EC63615F163A4FE96491E225D6DC3D9B9DACDF6190F883BEDCE94A97A0E4"=>"Soft rosy",
        "67AFDB094106138D548033A856F7286E8AA6D6E603C56107EE02C4EDAD8FFF35"=>"Rosy",_=>"Unknown"
    };
}

public sealed record AppearanceSettings
{
    public bool ShowOutline {get;init;}=false;
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
