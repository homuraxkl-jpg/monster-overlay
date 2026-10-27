namespace WhaleCompanion;

// Explicit target, not a guess from Steam UI or OS locale. Legacy installs default to chT.
public static class OfflineLanguage
{
    public static string Read(string root)
    {
        var path=Path.Combine(root,"玩家自定义","台词语言.txt");
        return File.Exists(path)?Normalize(File.ReadAllText(path).TrimStart('\uFEFF').Trim()):"chT";
    }
    public static string Normalize(string value)=>value.Trim() switch
    {
        "chS" or "简体中文" or "简中"=>"chS",
        "chT" or "繁體中文" or "繁体中文" or "繁中"=>"chT",
        "eng" or "en" or "English"=>"eng",
        _=>throw new InvalidDataException("台词语言支持“简体中文”、“繁體中文”或“English”，请修改玩家自定义/台词语言.txt。")
    };
    public static string Label(string language)=>Normalize(language) switch{"chS"=>"简体中文","chT"=>"繁體中文",_=>"English"};
    public static string FileName(string language)=>"ot_auto_"+Normalize(language)+".gmd";
    public static string TextName(string language)=>Normalize(language) switch{"chS"=>"随从台词-简体中文.txt","chT"=>"随从台词.txt",_=>"Palico-dialogue-English.txt"};
}
