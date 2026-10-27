using System.Text;
using System.Text.Json;
using WhaleCompanion;
Console.OutputEncoding=new UTF8Encoding(false);
try
{
    if(args.Length!=2||args[0] is not ("build" or "export" or "restore"))throw new InvalidDataException("请使用应用修改按钮启动工具。");
    var root=Path.GetFullPath(args[1]);var directory=Path.Combine(root,"config","dialogue");
    var language=OfflineLanguage.Read(root);var file=OfflineLanguage.FileName(language);
    var path=Path.Combine(directory,Path.ChangeExtension(file,".json"));
    var catalogBytes=File.ReadAllBytes(path);var hash=OfflineDialogue.Hash(catalogBytes);
    var catalog=JsonSerializer.Deserialize<OfflineCatalog>(catalogBytes)??throw new InvalidDataException("台词目录无法读取");
    var source=File.ReadAllBytes(Path.Combine(directory,"source",file));
    if(catalog.File!=file)throw new InvalidDataException("台词目录与所选语言不一致。");
    OfflineDialogue.Build(source,catalog);
    var text=Path.Combine(root,"玩家自定义",OfflineLanguage.TextName(language));
    Console.WriteLine("目标语言："+OfflineLanguage.Label(language)+"；编辑文件："+Path.GetFileName(text));
    if(args[0]=="export")
    {
        if(File.Exists(text))throw new InvalidDataException("文本台词已存在，不会覆盖玩家修改。");
        OfflineDialogue.AtomicWrite(text,OfflinePlainText.Export(catalog));Console.WriteLine("Exported editable dialogue text.");return;
    }
    var currentFriendly=File.ReadAllBytes(text);var friendlyHash=OfflineDialogue.Hash(currentFriendly);
    var friendlyBytes=args[0]=="restore"?File.ReadAllBytes(Path.Combine(directory,"defaults",OfflineLanguage.TextName(language))):currentFriendly;
    var updated=OfflinePlainText.Import(catalog,new UTF8Encoding(false,true).GetString(friendlyBytes));
    var result=OfflineDialogue.Build(source,updated);
    if(OfflineLanguage.Read(root)!=language||OfflineDialogue.Hash(File.ReadAllBytes(path))!=hash||OfflineDialogue.Hash(File.ReadAllBytes(text))!=friendlyHash)throw new InvalidDataException("语言或台词在处理过程中被其他程序修改，请保存后再试。");
    var output=Path.Combine(directory,"generated",file);Directory.CreateDirectory(Path.GetDirectoryName(output)!);
    File.Copy(path,path+".previous",true);OfflineDialogue.AtomicWrite(path,JsonSerializer.Serialize(updated,OfflineDialogue.JsonOptions));
    if(args[0]=="restore"){File.Copy(text,text+".previous",true);OfflineDialogue.AtomicWrite(text,new UTF8Encoding(false,true).GetString(friendlyBytes));}
    File.WriteAllBytes(output+".tmp",result);File.Move(output+".tmp",output,true);
    OfflineDialogue.AtomicWrite(Path.ChangeExtension(output,".validation.json"),JsonSerializer.Serialize(new{source_file=catalog.File,source_sha256=catalog.SourceHash,output_sha256=OfflineDialogue.Hash(result),changed_strings=updated.Entries.Count(x=>x.Replacement!=null&&x.Replacement!=x.Original),installed=false},OfflineDialogue.JsonOptions));
    Console.WriteLine("台词校验与生成完成。接下来应用到游戏文件。");
}
catch(Exception ex){Console.Error.WriteLine("未应用："+ex.Message);Environment.ExitCode=1;}
