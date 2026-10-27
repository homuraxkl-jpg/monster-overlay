using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ImGuiNET;

namespace WhaleCompanion;

public sealed class OfflineDialogueEditor : IDisposable
{
    private string DirectoryPath=>Path.Combine(CompanionChat.ConfigurationDirectory,"dialogue");
    private string language="chT";
    private string RootPath=>Path.GetDirectoryName(CompanionChat.ConfigurationDirectory)!;
    private string CatalogPath=>Path.Combine(DirectoryPath,Path.ChangeExtension(OfflineLanguage.FileName(language),".json"));
    private string SourcePath=>Path.Combine(DirectoryPath,"source",OfflineLanguage.FileName(language));
    private string PersonaPath=>Path.Combine(DirectoryPath,"offline-persona.md");
    private string PlayerPath=>Path.Combine(Path.GetDirectoryName(CompanionChat.ConfigurationDirectory)!,"玩家自定义");
    private string TextPath=>Path.Combine(PlayerPath,OfflineLanguage.TextName(language));
    private string PlayerPersonaPath=>Path.Combine(PlayerPath,"离线人设.txt");
    private string textHash="",requestTextHash="";
    private OfflineCatalog? catalog;
    private string catalogHash="",persona="",draft="",status="离线台词编辑器尚未加载";
    private bool loaded,useChatPersona=true;
    private int selected;
    private readonly HashSet<string> batch=new();
    private readonly HttpClient client=new(){Timeout=TimeSpan.FromSeconds(60)};
    private CancellationTokenSource? cancel;
    private Task<Dictionary<string,string>>? pending;
    private Dictionary<string,string>? proposal;
    private string requestHash="";

    private static string Fingerprint(string path)=>OfflineDialogue.Hash(File.ReadAllBytes(path));
    private void Load()
    {
        try
        {
            if(pending!=null)return;
            catalog=null;language=OfflineLanguage.Read(RootPath);
            var text=File.ReadAllText(CatalogPath);if(text.Length>1000000)throw new InvalidDataException("目录太大");
            var next=JsonSerializer.Deserialize<OfflineCatalog>(text)??throw new InvalidDataException("目录错误");
            if(next.File!=OfflineLanguage.FileName(language)||next.Entries.Count==0)throw new InvalidDataException("目录语言错误");
            if(File.Exists(TextPath))next=OfflinePlainText.Import(next,File.ReadAllText(TextPath));
            OfflineDialogue.Build(File.ReadAllBytes(SourcePath),next);
            catalog=next;catalogHash=Fingerprint(CatalogPath);selected=Math.Max(0,next.Entries.FindIndex(x=>x.Replacement!=null));draft=next.Entries[selected].Replacement??"";
            textHash=File.Exists(TextPath)?Fingerprint(TextPath):"";
            useChatPersona=!File.Exists(PlayerPersonaPath)&&!File.Exists(PersonaPath);
            persona=useChatPersona?ChatConfiguration.Load(CompanionChat.ConfigurationDirectory).Persona:File.ReadAllText(File.Exists(PlayerPersonaPath)?PlayerPersonaPath:PersonaPath);
            batch.Clear();foreach(var line in next.Entries.Where(x=>x.Replacement!=null).Take(8))batch.Add(line.Key);
            proposal=null;status="已加载"+OfflineLanguage.Label(language)+"台词表；生成文件不会直接安装到游戏";
        }
        catch(Exception ex){status="加载失败："+ex.Message;}
        loaded=true;
    }
    private void Save(OfflineCatalog next)
    {
        if(OfflineLanguage.Read(RootPath)!=language)throw new InvalidDataException("目标语言已被外部修改，请重新加载");
        if(Fingerprint(CatalogPath)!=catalogHash)throw new InvalidDataException("文件被外部修改，请先重新加载");
        if((File.Exists(TextPath)?Fingerprint(TextPath):"")!=textHash)throw new InvalidDataException("文本台词已被外部修改，请先重新加载");
        var source=File.ReadAllBytes(SourcePath);var bytes=OfflineDialogue.Build(source,next);
        var generated=Path.Combine(DirectoryPath,"generated",OfflineLanguage.FileName(language));
        File.Copy(CatalogPath,CatalogPath+".previous",true);
        OfflineDialogue.AtomicWrite(CatalogPath,JsonSerializer.Serialize(next,OfflineDialogue.JsonOptions));
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(generated)!);
        var temp=generated+".tmp";File.WriteAllBytes(temp,bytes);File.Move(temp,generated,true);
        OfflineDialogue.AtomicWrite(Path.ChangeExtension(generated,".validation.json"),JsonSerializer.Serialize(new{
            source_file=next.File,source_sha256=next.SourceHash,output_sha256=OfflineDialogue.Hash(bytes),
            changed_strings=next.Entries.Count(x=>x.Replacement!=null&&x.Replacement!=x.Original),installed=false},OfflineDialogue.JsonOptions));
        catalog=next;catalogHash=Fingerprint(CatalogPath);draft=next.Entries[selected].Replacement??"";
        if(File.Exists(TextPath))File.Copy(TextPath,TextPath+".previous",true);
        OfflineDialogue.AtomicWrite(TextPath,OfflinePlainText.Export(next));textHash=Fingerprint(TextPath);
        proposal=null;status="已保存JSON并生成GMD候选；退出游戏后运行应用脚本，再重启加载";
    }
    private OfflineCatalog Copy()=>JsonSerializer.Deserialize<OfflineCatalog>(JsonSerializer.Serialize(catalog))!;
    private void Generate()
    {
        if(catalog==null||pending!=null)return;
        try
        {
            var lines=catalog.Entries.Where(x=>batch.Contains(x.Key)).Select(x=>new OfflineLine{Key=x.Key,Original=x.Original,Index=x.Index}).ToArray();
            if(lines.Length is <1 or >8)throw new InvalidDataException("每批请选择1至8句");
            if(OfflineLanguage.Read(RootPath)!=language)throw new InvalidDataException("目标语言已变更，请重新加载");
            if(Fingerprint(CatalogPath)!=catalogHash)throw new InvalidDataException("目录已被外部修改，请重新加载");
            if((File.Exists(TextPath)?Fingerprint(TextPath):"")!=textHash)throw new InvalidDataException("文本台词已变更，请重新加载");
            var config=ChatConfiguration.Load(CompanionChat.ConfigurationDirectory);
            var style=useChatPersona?config.Persona:persona;
            requestHash=catalogHash;requestTextHash=textHash;proposal=null;cancel?.Dispose();cancel=new();
            pending=Request(config.Settings.Model,style,lines,cancel.Token,config.Settings.Endpoint,language);
            status=$"正在生成{lines.Length}句（1次付费请求，无自动重试）；不发送聊天记忆";
        }
        catch(Exception ex){status="未生成："+ex.Message;}
    }
    private async Task<Dictionary<string,string>> Request(string model,string style,OfflineLine[] lines,CancellationToken token,string endpoint,string targetLanguage)
    {
        var bytes=ProtectedData.Unprotect(await File.ReadAllBytesAsync(CompanionChat.SecretPath,token),null,DataProtectionScope.CurrentUser);
        string key;try{key=Encoding.UTF8.GetString(bytes);}finally{CryptographicOperations.ZeroMemory(bytes);}
        return await OfflineDialogue.Generate(client,key,model,style,lines,token,endpoint,targetLanguage);
    }
    public void Poll()
    {
        if(pending is not {IsCompleted:true})return;
        try
        {
            var result=pending.GetAwaiter().GetResult();
            if(OfflineLanguage.Read(RootPath)!=language||Fingerprint(CatalogPath)!=requestHash||(File.Exists(TextPath)?Fingerprint(TextPath):"")!=requestTextHash)throw new InvalidDataException("生成期间语言或台词已被外部修改，结果作废");
            proposal=result;status="生成完成，请查看候选后保存；尚未覆盖台词表或游戏文件";
        }
        catch(OperationCanceledException){status="已取消生成，原台词表保留";}
        catch(Exception ex){status="生成未保存："+ex.GetBaseException().Message;}
        finally{pending=null;cancel?.Dispose();cancel=null;}
    }
    public void Render()
    {
        if(!ImGui.CollapsingHeader(UiLanguage.Text("Offline Palico dialogue / persona editor","原生离线台词与人设编辑器","原生離線台詞與人設編輯器")))return;
        if(!loaded)Load();
        ImGui.TextWrapped(status);
        ImGui.TextWrapped("目标语言必须与游戏文本语言一致；此选择不会修改游戏设置。三种语言分别保存。当前文件：玩家自定义/"+OfflineLanguage.TextName(language));
        ImGui.TextWrapped("保存并退出游戏后双击应用修改；离线文本仍需重启加载。");
        if(pending!=null)
        {if(ImGui.Button(UiLanguage.Text("Cancel offline generation","取消离线生成","取消離線產生")))cancel?.Cancel();return;}
        var languageIndex=language=="chS"?0:language=="chT"?1:2;
        if(ImGui.Combo(UiLanguage.Text("Target text language","原生文本目标语言","原生文字目標語言"),ref languageIndex,new[]{"简体中文 (chS)","繁體中文 (chT)","English (eng)"},3))
        {
            try{OfflineDialogue.AtomicWrite(Path.Combine(PlayerPath,"台词语言.txt"),languageIndex==0?"简体中文":languageIndex==1?"繁體中文":"English");Load();}
            catch(Exception ex){status="语言切换失败："+ex.Message;}
        }
        if(ImGui.Button(UiLanguage.Text("Reload offline files","重新加载离线文件","重新載入離線檔案")))Load();
        if(catalog==null)return;
        ImGui.Checkbox(UiLanguage.Text("Use current chat persona.md","使用聊天人设persona.md","使用聊天人設persona.md"),ref useChatPersona);
        if(!useChatPersona)
        {
            ImGui.InputTextMultiline(UiLanguage.Text("Offline persona","独立离线人设","獨立離線人設"),ref persona,16001,new System.Numerics.Vector2(600,130));
            if(ImGui.Button(UiLanguage.Text("Save offline persona","保存离线人设","儲存離線人設")))
            {try{if(string.IsNullOrWhiteSpace(persona)||persona.Length>16000)throw new InvalidDataException("人设为空或过长");OfflineDialogue.AtomicWrite(PlayerPersonaPath,persona);status="独立离线人设已保存到玩家自定义/离线人设.txt，聊天人设未改";}catch(Exception ex){status=ex.Message;}}
        }
        var previous=selected;
        ImGui.InputInt(UiLanguage.Text("Entry position","台词位置","台詞位置"),ref selected);selected=Math.Clamp(selected,0,catalog.Entries.Count-1);
        if(previous!=selected)draft=catalog.Entries[selected].Replacement??"";
        var row=catalog.Entries[selected];
        ImGui.TextWrapped($"{selected+1}/{catalog.Entries.Count} | {row.Key}\n原句：{row.Original}");
        ImGui.InputTextMultiline(UiLanguage.Text("Replacement (manual)","手动替换文本","手動替換文字"),ref draft,1024,new System.Numerics.Vector2(600,80));
        if(ImGui.Button(UiLanguage.Text("Save line and build candidate","保存此句并生成候选","儲存此句並產生候選")))
        {try{var next=Copy();next.Entries[selected].Replacement=draft;Save(next);}catch(Exception ex){status=ex.Message;}}
        ImGui.SameLine();
        if(ImGui.Button("Restore this original line"))
        {try{var next=Copy();next.Entries[selected].Replacement=null;Save(next);}catch(Exception ex){status=ex.Message;}}
        bool include=batch.Contains(row.Key);
        if(ImGui.Checkbox("Include this entry in AI batch",ref include)){if(include)batch.Add(row.Key);else batch.Remove(row.Key);}
        if(ImGui.Button("Use this entry only")){batch.Clear();batch.Add(row.Key);}
        ImGui.SameLine();if(ImGui.Button("Clear AI batch"))batch.Clear();
        ImGui.TextUnformatted($"AI batch: {batch.Count} entries (1-8 per request). Uses encrypted chat key and configured Model.");
        if(ImGui.Button("Generate offline batch (paid API, preview only)"))Generate();
        if(proposal!=null)
        {
            foreach(var pair in proposal)ImGui.TextWrapped(pair.Key+": "+pair.Value);
            if(ImGui.Button("Save generated batch and build candidate"))
            {try{var next=Copy();foreach(var line in next.Entries)if(proposal.TryGetValue(line.Key,out var text))line.Replacement=text;Save(next);}catch(Exception ex){status=ex.Message;}}
            ImGui.SameLine();if(ImGui.Button("Discard generated batch")){proposal=null;status="生成候选已丢弃";}
        }
        if(ImGui.Button("Build current catalog (no API)")){try{Save(Copy());}catch(Exception ex){status=ex.Message;}}
    }
    public void Dispose(){cancel?.Cancel();client.Dispose();}
}
