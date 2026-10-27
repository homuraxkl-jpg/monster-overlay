using ImGuiNET;
namespace WhaleCompanion;
public partial class ProbePlugin
{
    private int nativeOutlineCaptureBusy,nativeOutlineCaptureCompleted;
    private bool nativeOutlineAutoPending=true;
    private double nextOutlineSceneCapture;
    private int outlineSceneCaptures;
    private void ObserveNativeOutlineScene()
    {
        if(weaponStateRevision!="421810"||outlineSceneCaptures>=6||clock.Elapsed.TotalSeconds<nextOutlineSceneCapture||!HasLoadedSave())return;
        nextOutlineSceneCapture=clock.Elapsed.TotalSeconds+5;
        try
        {
            var manager=(ulong)(SharpPluginLoader.Core.SingletonManager.GetSingleton("sMhUnit")?.Instance??0);
            var report=NativeOutlineSceneProbe.Capture(localMemory,manager,gameModule,palicoHealthProbe.LatestHealth,clock.Elapsed.TotalSeconds,weaponStateRevision);
            var directory=Path.Combine(CompanionChat.ConfigurationDirectory,"research");Directory.CreateDirectory(directory);
            OfflineDialogue.AtomicWrite(Path.Combine(directory,"native-outline-scene-v2-"+outlineSceneCaptures+".json"),System.Text.Json.JsonSerializer.Serialize(report,new System.Text.Json.JsonSerializerOptions{WriteIndented=true}));
            outlineSceneCaptures++;
            WriteLog($"NATIVE_OUTLINE_SCENE_V2 sample={outlineSceneCaptures} units={report.Units} filters={report.Filters.Length} cat_component={report.PalicoHasComponent} read_failures={report.ReadFailures}");
        }
        catch(Exception ex){WriteLog("NATIVE_OUTLINE_SCENE unavailable="+ex.GetType().Name);outlineSceneCaptures++;}
    }
    private void MaybeCaptureNativeOutline()
    {
        ObserveNativeOutlineScene();
        if(Interlocked.Exchange(ref nativeOutlineCaptureCompleted,0)!=0)WriteLog("NATIVE_OUTLINE_RESEARCH "+Volatile.Read(ref nativeOutlineCaptureStatus));
        if(!nativeOutlineAutoPending)return;nativeOutlineAutoPending=false;
        try{if(weaponStateRevision=="421810"&&!File.Exists(Path.Combine(CompanionChat.ConfigurationDirectory,"research","native-outline-421810-v1.json")))StartNativeOutlineCapture();}
        catch{nativeOutlineCaptureStatus="Native outline capture unavailable";}
    }
    private string nativeOutlineCaptureStatus="Native outline renderer not captured";
    private void StartNativeOutlineCapture()
    {
        if(Interlocked.Exchange(ref nativeOutlineCaptureBusy,1)!=0)return;
        var revision=weaponStateRevision;
        _=Task.Run(()=>{try{Interlocked.Exchange(ref nativeOutlineCaptureStatus,NativeOutlineResearch.Export(Path.Combine(CompanionChat.ConfigurationDirectory,"research"),revision));}catch(Exception ex){Interlocked.Exchange(ref nativeOutlineCaptureStatus,"Native outline capture failed: "+ex.GetType().Name);}finally{Volatile.Write(ref nativeOutlineCaptureBusy,0);Interlocked.Exchange(ref nativeOutlineCaptureCompleted,1);}});
    }
    private void RenderAppearanceControls()
    {
        if(!ImGui.CollapsingHeader(UiLanguage.Text("Appearance","外观","外觀")))return;
        if(ImGui.Checkbox(UiLanguage.Text("Show head pot","显示头顶铁盆","顯示頭頂鐵盆"),ref showHeadPot))SaveAppearance();
        ImGui.TextWrapped("Face tint: "+face.FaceTint);
        ImGui.TextWrapped(UiLanguage.Text("Model outline is deferred for player feedback; research is disabled in this release.","模型描边留待玩家反馈后处理；本候选关闭相关研究。","模型描邊留待玩家回饋後處理；本候選關閉相關研究。"));
        ImGui.TextWrapped(UiLanguage.Text("Face tint: close the game, then run Install.ps1 -FaceTint original / soft-rosy / rosy from the release package.","脸部调色：退出游戏后运行发布包的 Install.ps1 -FaceTint original / soft-rosy / rosy。","臉部調色：退出遊戲後執行發佈包的 Install.ps1 -FaceTint original / soft-rosy / rosy。"));
        ImGui.TextWrapped(appearanceStatus);
    }
    private void SaveAppearance()
    {
        face.SetPotVisible(showHeadPot);face.SetOutlineVisible(false);
        try{new AppearanceSettings{ShowHeadPot=showHeadPot,ShowOutline=false}.Save(AppearancePath);appearanceStatus="Appearance saved";}
        catch{appearanceStatus="Applied for this session; could not save appearance";}
    }
}
