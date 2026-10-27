using System.Text.Json;

namespace WhaleCompanion;

// Local, user-triggered export. No network, chat, names, addresses, or credentials.
public sealed class CombatFeedback
{
    readonly Queue<CombatDamageResult> results=new();
    public void Record(CombatDamageResult result){if(results.Count>=200)results.Dequeue();results.Enqueue(result);}
    public string Export(string directory,string revision)
    {
        Directory.CreateDirectory(directory);
        var path=Path.Combine(directory,"combat-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff")+".json");
        var payload=new{plugin="P11.5",game_revision=revision,created_utc=DateTime.UtcNow,estimated=true,results=results.ToArray()};
        File.WriteAllText(path,JsonSerializer.Serialize(payload,new JsonSerializerOptions{WriteIndented=true}));return path;
    }
}
