using ImGuiNET;
namespace WhaleCompanion;
public sealed partial class ProbePlugin
{
    string selectedResponse="",offlineDraft="",promptDraft="";
    private void EnableResponseDetection(bool value)
    {
        enabled=autoCompanion=actionCompanion=allWeaponCompanion=spiritCompanion=weaponStateDiagnostic=saedResponses=value;
        actionDirector.IncludeAllWeapons=value;
        if(value){scenePaused=false;ApplyConversationOptions(false);}
    }
    private void SetAllResponses(bool value)
    {
        responses.Settings.Enabled=value;
        EnableResponseDetection(value);
        foreach(var group in responses.Settings.Categories.Values)group.Enabled=value;
        if(!value){chat.CancelAutomatic();responses.ResetScene();}
        responses.Save();
    }
    private static string CategoryLabel(string key)=>key switch
    {
        "health"=>UiLanguage.Text("Health","生命状态","生命狀態"),"evasion"=>UiLanguage.Text("Evasion","回避","迴避"),
        "guard"=>UiLanguage.Text("Guard preparation","防御后准备","防禦後準備"),"spirit"=>UiLanguage.Text("Spirit level (no cooldown)","太刀升刃（无冷却）","太刀升刃（無冷卻）"),
        "monster"=>UiLanguage.Text("Monster state","怪物状态","怪物狀態"),"damage"=>UiLanguage.Text("Damage verdicts","伤害评价","傷害評價"),
        "weapon"=>UiLanguage.Text("Weapon actions","武器动作","武器動作"),"buff"=>UiLanguage.Text("Actual buffs","实际增益","實際增益"),
        "quest"=>UiLanguage.Text("Quest result","任务结果","任務結果"),_=>key
    };
    private void RenderResponseControls()
    {
        var languagePath=Path.Combine(CompanionChat.ConfigurationDirectory,"ui-language.txt");
        if(ImGui.Button("English"))UiLanguage.Set("eng",languagePath);
        ImGui.SameLine();if(ImGui.Button("简体中文"))UiLanguage.Set("chS",languagePath);
        ImGui.SameLine();if(ImGui.Button("繁體中文"))UiLanguage.Set("chT",languagePath);
        if(ImGui.Button(UiLanguage.Text("Enable all companion categories","一键开启全部陪伴","一鍵開啟全部陪伴")))SetAllResponses(true);
        ImGui.SameLine();if(ImGui.Button(UiLanguage.Text("Disable all companion categories","一键关闭全部陪伴","一鍵關閉全部陪伴")))SetAllResponses(false);
        bool dynamicAi=responses.Settings.DynamicAi;
        if(ImGui.Checkbox(UiLanguage.Text("Dynamic AI rewriting (paid requests)","AI动态改写（产生API请求）","AI動態改寫（產生API請求）"),ref dynamicAi)){responses.Settings.DynamicAi=dynamicAi;chat.CancelAutomatic();responses.Save();}
        if(!ImGui.CollapsingHeader(UiLanguage.Text("Response categories and fixed lines","分类开关与固定台词","分類開關與固定台詞")))return;
        foreach(var (key,group) in responses.Settings.Categories)
        {
            ImGui.PushID(key);bool on=group.Enabled;
            if(ImGui.Checkbox(CategoryLabel(key),ref on)){group.Enabled=on;if(on){responses.Settings.Enabled=true;EnableResponseDetection(true);}responses.Save();chat.CancelAutomatic();}
            if(key=="spirit"){ImGui.PopID();continue;}
            ImGui.SameLine();float cooldown=(float)group.CooldownSeconds;
            if(ImGui.InputFloat(UiLanguage.Text("Shared cooldown (seconds)","类别共享冷却（秒）","類別共用冷卻（秒）"),ref cooldown)&&float.IsFinite(cooldown)&&cooldown>=0&&cooldown<=3600){group.CooldownSeconds=cooldown;responses.Save();}
            ImGui.PopID();
        }
        if(ImGui.TreeNode(UiLanguage.Text("Output timing (seconds)","输出时序（秒）","輸出時序（秒）")))
        {
            var timing=responses.Settings.Thresholds;
            foreach(var item in new (string Name,double Value,Action<double> Set,double Minimum)[]{
                ("Event freshness",timing.EventFreshnessSeconds,v=>timing.EventFreshnessSeconds=v,.1),
                ("AI event timeout",timing.DynamicEventTimeoutSeconds,v=>timing.DynamicEventTimeoutSeconds=v,.1),
                ("Local message interval",timing.NativeOutputIntervalSeconds,v=>timing.NativeOutputIntervalSeconds=v,0),
                ("Multiplayer send interval",timing.SharedOutputIntervalSeconds,v=>timing.SharedOutputIntervalSeconds=v,0),
                ("Quiet time after AI reply",timing.AiReplyQuietSeconds,v=>timing.AiReplyQuietSeconds=v,0),
                ("Combat verdict max age",timing.CombatResultMaxAgeSeconds,v=>timing.CombatResultMaxAgeSeconds=v,.1)})
            {float value=(float)item.Value;if(ImGui.InputFloat(item.Name,ref value)&&float.IsFinite(value)&&value>=item.Minimum&&value<=3600){item.Set(value);responses.Save();}}
            ImGui.TextWrapped("Only the newest unsent line is kept. Increasing freshness may allow old-action remarks.");
            ImGui.TreePop();
        }
        if(ImGui.Button(UiLanguage.Text("Reload external response settings","重新加载外置回应配置","重新載入外置回應設定"))){responses.Load(Path.Combine(CompanionChat.ConfigurationDirectory,"responses.json"));chat.CancelAutomatic();selectedResponse="";}
        if(ImGui.Button(UiLanguage.Text("Save last lines as offline defaults","把各状态最后一句固定为离线台词","將各狀態最後一句固定為離線台詞"))){responses.FreezeLast();selectedResponse="";}
        ImGui.BeginChild("ResponseEventList",new System.Numerics.Vector2(650,180),(ImGuiChildFlags)0);
        foreach(var (id,rule) in responses.Settings.Events.ToArray())
            if(ImGui.Selectable(id,selectedResponse==id)){selectedResponse=id;offlineDraft=string.IsNullOrWhiteSpace(rule.OfflineText)?rule.ReferenceText:rule.OfflineText;promptDraft=rule.Prompt;}
        ImGui.EndChild();
        if(responses.Settings.Events.TryGetValue(selectedResponse,out var selected))
        {
            bool active=selected.Enabled;if(ImGui.Checkbox(UiLanguage.Text("Enable selected event","启用此事件","啟用此事件"),ref active)){selected.Enabled=active;responses.Save();}
            ImGui.InputTextMultiline(UiLanguage.Text("Offline fallback line","离线固定台词","離線固定台詞"),ref offlineDraft,8000,new System.Numerics.Vector2(650,80));
            ImGui.InputTextMultiline(UiLanguage.Text("AI event wording instructions","AI事件话术要求","AI事件話術要求"),ref promptDraft,32000,new System.Numerics.Vector2(650,120));
            if(ImGui.Button(UiLanguage.Text("Save selected event","保存此事件","儲存此事件"))&&offlineDraft.Length<=2000&&promptDraft.Length<=8000){selected.OfflineText=offlineDraft;selected.Prompt=promptDraft;responses.Save();}
        }
        ImGui.TextUnformatted(responses.Status);
    }
}

