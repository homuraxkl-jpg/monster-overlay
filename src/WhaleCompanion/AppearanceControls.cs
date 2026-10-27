using ImGuiNET;
namespace WhaleCompanion;
public partial class ProbePlugin
{
    private void RenderAppearanceControls()
    {
        if(!ImGui.CollapsingHeader(UiLanguage.Text("Appearance","外观","外觀")))return;
        if(ImGui.Checkbox(UiLanguage.Text("Show head pot","显示头顶铁盆","顯示頭頂鐵盆"),ref showHeadPot))SaveAppearance();
        if(!face.OutlineAvailable || !face.Ready)ImGui.BeginDisabled();
        if(ImGui.Checkbox(UiLanguage.Text("Optional model outline","可选模型描边","可選模型描邊"),ref showOutline))SaveAppearance();
        if(!face.OutlineAvailable || !face.Ready)ImGui.EndDisabled();
        ImGui.TextWrapped("Face tint: "+face.FaceTint);
        ImGui.TextWrapped(UiLanguage.Text("Face tint presets require closing the game and running Install-OptionalAppearance.ps1 -Tint original / soft-rosy / rosy. Outline is experimental and defaults OFF.","脸部调色需退出游戏后运行 Install-OptionalAppearance.ps1 -Tint original / soft-rosy / rosy。描边为实验选项，默认关闭。","臉部調色需退出遊戲後執行 Install-OptionalAppearance.ps1 -Tint original / soft-rosy / rosy。描邊為實驗選項，預設關閉。"));
        if(!face.OutlineAvailable || !face.Ready)ImGui.TextWrapped("Outline asset is not installed; existing appearance retained.");
        ImGui.TextWrapped(appearanceStatus);
    }
    private void SaveAppearance()
    {
        face.SetPotVisible(showHeadPot);face.SetOutlineVisible(showOutline);
        try{new AppearanceSettings{ShowHeadPot=showHeadPot,ShowOutline=showOutline}.Save(AppearancePath);appearanceStatus="Appearance saved";}
        catch{appearanceStatus="Applied for this session; could not save appearance";}
    }
}
