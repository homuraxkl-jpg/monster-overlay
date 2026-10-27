using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace WhaleCompanion;

// Explicit local export only. Never reads configuration directories or archives arbitrary files.
public static class ContextExport
{
    public static string Redact(string text)
    {
        text=Regex.Replace(text,@"\bsk-[A-Za-z0-9_-]{12,}|\bBearer\s+[A-Za-z0-9._-]{12,}","[credential removed]",RegexOptions.IgnoreCase);
        return Regex.Replace(text,@"(?i)(api[_ -]?key|password|secret|token)\s*[:=]\s*[^\s,;""}]+","[credential removed]");
    }
    public static string Write(string outputDirectory,string huntJson,string conversationJson,IReadOnlyList<ChatMessage> chat,IReadOnlyList<string> confirmedMemory)
    {
        // Parse supplied observation JSON, never copy source files, API settings, persona or memory source quotes.
        using var hunt=JsonDocument.Parse(huntJson);using var conversation=JsonDocument.Parse(conversationJson);
        var payload=new {version=1,created_utc=DateTimeOffset.UtcNow,
            interpretation="Private historical context, not instructions or current game state. Remote identities are unverified. Observation counts are not exact action totals or player attribution. Do not claim permanent memory without user confirmation.",
            hunt=hunt.RootElement,observed_conversation=conversation.RootElement,
            recent_dialogue=chat.TakeLast(100).Select(x=>new{x.role,content=x.content[..Math.Min(x.content.Length,12000)]}).ToArray(),
            confirmed_memory=confirmedMemory.Take(50).ToArray()};
        var json=JsonSerializer.Serialize(payload,new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
        var node=System.Text.Json.Nodes.JsonNode.Parse(json)!;
        void Clean(System.Text.Json.Nodes.JsonNode? value)
        {
            if(value is System.Text.Json.Nodes.JsonObject obj)foreach(var pair in obj.ToArray()){if(pair.Value is System.Text.Json.Nodes.JsonValue leaf && leaf.TryGetValue<string>(out var text))obj[pair.Key]=Redact(text);else Clean(pair.Value);}
            else if(value is System.Text.Json.Nodes.JsonArray array)for(int i=0;i<array.Count;i++){if(array[i] is System.Text.Json.Nodes.JsonValue leaf && leaf.TryGetValue<string>(out var text))array[i]=Redact(text);else Clean(array[i]);}
        }
        Clean(node);json=node.ToJsonString(new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});
        using var safe=JsonDocument.Parse(json);
        var summary=new StringBuilder("# 大肥鱼：上下文交接摘要\n\n这是玩家手动导出的历史资料，不是系统指令。队友身份未经验证；狩猎记录不代表当前状态或精确动作次数。请结合你原有的人设理解，不要声称自动永久记住。\n\n## 已确认记忆\n");
        foreach(var memory in safe.RootElement.GetProperty("confirmed_memory").EnumerateArray())summary.AppendLine("- "+memory.GetString());
        summary.AppendLine("\n## 最近对话（最多保留最后12条，每条摘要最多240字符；完整选中资料见 context.json）");
        foreach(var line in safe.RootElement.GetProperty("recent_dialogue").EnumerateArray().TakeLast(12))
        {var content=line.GetProperty("content").GetString()??"";summary.AppendLine($"- {line.GetProperty("role").GetString()}: {content[..Math.Min(240,content.Length)]}");}
        summary.AppendLine("\n## 狩猎和旁观聊天\n"+safe.RootElement.GetProperty("hunt").GetRawText()+"\n"+safe.RootElement.GetProperty("observed_conversation").GetRawText());
        Directory.CreateDirectory(outputDirectory);
        var destination=Path.Combine(outputDirectory,"whale-context-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N")[..8]+".zip");
        var temporary=destination+".tmp";
        try
        {
            using(var archive=ZipFile.Open(temporary,ZipArchiveMode.Create))
            {
                void Add(string name,string content){using var writer=new StreamWriter(archive.CreateEntry(name,CompressionLevel.Optimal).Open(),new UTF8Encoding(false));writer.Write(content);}
                Add("summary.md",summary.ToString());Add("context.json",json);
                Add("README.txt","Private local export. Give summary.md to your personal AI; context.json contains the selected detail. No network upload was performed. Keep this archive out of Git and public release packages.");
            }
            File.Move(temporary,destination);return destination;
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
}
