using ImGuiNET;
namespace WhaleCompanion;
public partial class ProbePlugin
{
    private int nativeOutlineCaptureBusy,nativeOutlineCaptureCompleted;
    private bool nativeOutlineAutoPending=true;
    private void MaybeCaptureNativeOutline()
    {
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
        if(!face.OutlineAvailable || !face.Ready)ImGui.BeginDisabled();
        if(ImGui.Checkbox(UiLanguage.Text("Legacy outline shell (no visible effect confirmed)","旧轮廓壳（实测未见效果）","舊輪廓殼（實測未見效果）"),ref showOutline))SaveAppearance();
        if(!face.OutlineAvailable || !face.Ready)ImGui.EndDisabled();
        ImGui.TextWrapped("Face tint: "+face.FaceTint);
        ImGui.TextWrapped(UiLanguage.Text("Face tint presets require closing the game and running Install-OptionalAppearance.ps1 -Tint original / soft-rosy / rosy. Outline is experimental and defaults OFF.","脸部调色需退出游戏后运行 Install-OptionalAppearance.ps1 -Tint original / soft-rosy / rosy。描边为实验选项，默认关闭。","臉部調色需退出遊戲後執行 Install-OptionalAppearance.ps1 -Tint original / soft-rosy / rosy。描邊為實驗選項，預設關閉。"));
        if(!face.OutlineAvailable || !face.Ready)ImGui.TextWrapped("Outline asset is not installed; existing appearance retained.");
        if(ImGui.Button("Capture native outline renderer (read only)"))StartNativeOutlineCapture();
        ImGui.TextWrapped(Volatile.Read(ref nativeOutlineCaptureStatus));
        ImGui.TextWrapped("Native component route is research only; no effect or game memory writes enabled.");
        ImGui.TextWrapped(appearanceStatus);
    }
    private void SaveAppearance()
    {
        face.SetPotVisible(showHeadPot);face.SetOutlineVisible(showOutline);
        try{new AppearanceSettings{ShowHeadPot=showHeadPot,ShowOutline=showOutline}.Save(AppearancePath);appearanceStatus="Appearance saved";}
        catch{appearanceStatus="Applied for this session; could not save appearance";}
    }
}
