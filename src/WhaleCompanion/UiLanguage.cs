using System.Globalization;
namespace WhaleCompanion;
public static class UiLanguage
{
    public static string Current {get;private set;}=Detect(CultureInfo.CurrentUICulture.Name);
    public static string Detect(string locale)=>locale.StartsWith("zh",StringComparison.OrdinalIgnoreCase)?(locale.Contains("TW",StringComparison.OrdinalIgnoreCase)||locale.Contains("HK",StringComparison.OrdinalIgnoreCase)||locale.Contains("Hant",StringComparison.OrdinalIgnoreCase)?"chT":"chS"):"eng";
    public static string Text(string english,string simplified,string traditional)=>Current switch{"chS"=>simplified+"###"+english,"chT"=>traditional+"###"+english,_=>english+"###"+english};
    public static string OutputInstruction=>Current switch{"chS"=>"使用简体中文。","chT"=>"使用繁體中文。",_=>"Reply in English."};
    public static void Load(string path){try{if(File.Exists(path))Set(File.ReadAllText(path).Trim(),null);}catch{}}
    public static void Set(string language,string? path)
    {
        if(language is not ("chS" or "chT" or "eng"))return;
        if(path!=null)OfflineDialogue.AtomicWrite(path,language);
        Current=language;
    }
}
