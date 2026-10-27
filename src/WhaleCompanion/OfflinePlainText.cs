using System.Text;
using System.Text.Json;

namespace WhaleCompanion;

// Human-facing format: Chinese headings and ordinary multiline text, no JSON/key edits.
public static class OfflinePlainText
{
    public static string Heading(OfflineLine row,bool english=false)
    {
        if(english)return $"【Entry {row.Index+1:000}】";
        var group=row.Index<17?"行动与恢复":row.Index<39?"伙伴互动":row.Index<85?"支援道具":row.Index<204?"探索与任务":"随从闲聊";
        return $"【{group} · 第{row.Index+1:000}句】";
    }
    private static Dictionary<string,string> Markers(OfflineLine row,bool english=false)
    {
        var result=new Dictionary<string,string>();int number=0;
        foreach(var token in OfflineDialogue.FormatTokens(row.Original).Distinct())
            result.Add(token,token=="%s"?(english?"〔Name〕":"〔名字〕"):token=="<ICON NOTE>"?(english?"〔Note〕":"〔音符〕"):english?$"〔Parameter{++number}〕":$"〔参数{++number}〕");
        return result;
    }
    public static string Export(OfflineCatalog catalog)
    {
        bool english=catalog.File.EndsWith("_eng.gmd",StringComparison.OrdinalIgnoreCase);
        var text=new StringBuilder(english?"# Edit dialogue below each heading. Keep headings and markers unchanged.\n# Use (keep original) to restore one entry. Exit the game before applying.\n\n":"# 只修改标题下方的台词，标题保持不变。可以直接换行。\n# 〔名字〕、〔音符〕等小标记请保留；不需要编写代码。\n# 写（保留原句）可恢复这一句。保存后退出游戏，双击“应用修改”。\n\n");
        foreach(var row in catalog.Entries)
        {
            var value=row.Replacement??row.Original;
            foreach(var pair in Markers(row,english))value=value.Replace(pair.Key,pair.Value,StringComparison.Ordinal);
            text.AppendLine(Heading(row,english));text.AppendLine(value.Replace("\r\n","\n"));text.AppendLine();
        }
        return text.ToString().Replace("\r\n","\n");
    }
    public static OfflineCatalog Import(OfflineCatalog catalog,string text)
    {
        if(text.Length>1000000)throw new InvalidDataException("台词文件过大");
        var next=JsonSerializer.Deserialize<OfflineCatalog>(JsonSerializer.Serialize(catalog))!;
        bool english=catalog.File.EndsWith("_eng.gmd",StringComparison.OrdinalIgnoreCase);
        var headings=next.Entries.ToDictionary(row=>Heading(row,english));
        var bodies=new Dictionary<string,List<string>>();string? current=null;
        foreach(var line in text.TrimStart('\uFEFF').Replace("\r\n","\n").Split('\n'))
        {
            if(line.StartsWith('【')&&line.EndsWith('】'))
            {
                if(!headings.ContainsKey(line)||!bodies.TryAdd(line,new()))throw new InvalidDataException("标题被改动或重复："+line);
                current=line;continue;
            }
            if(current==null){if(line.Length>0&&!line.StartsWith('#'))throw new InvalidDataException("台词必须写在原有标题下方");continue;}
            bodies[current].Add(line);
        }
        if(bodies.Count!=headings.Count)throw new InvalidDataException("有标题被删除，请从备份恢复");
        foreach(var (heading,row) in headings)
        {
            var value=string.Join("\n",bodies[heading]).Trim();
            if(value is "（保留原句）" or "(keep original)"){row.Replacement=null;continue;}
            foreach(var pair in Markers(row,english))value=value.Replace(pair.Value,pair.Key,StringComparison.Ordinal);
            try{OfflineDialogue.ValidateText(row.Original,value);}catch(Exception ex){throw new InvalidDataException(heading+"："+ex.Message+"。请保留名字/音符标记。",ex);}
            // Native source uses CRLF; normalize edited line endings consistently.
            value=value.Replace("\n","\r\n");
            row.Replacement=value==row.Original||(row.Replacement==null&&value.Trim()==row.Original.Trim())?null:value;
        }
        return next;
    }
}
