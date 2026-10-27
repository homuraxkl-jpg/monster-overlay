using SharpPluginLoader.Core.Entities;
namespace WhaleCompanion;
public sealed partial class ProbePlugin
{
    private readonly HuntOutcomeDirector huntOutcomes=new();
    public void OnMonsterDeath(Monster monster)
    {
        if(!enabled||scenePaused||!autoCompanion||weaponStateRevision!="421810")return;
        // No Is/GetDti/native getter, no retained wrapper and no dialogue/API call in the hook.
        ulong address=(ulong)monster.Instance;
        if(LocalProcessMemory.IsAddress(address,0x12284)&&localMemory.Int32(address+0x12280,out var type)&&HuntOutcomeDirector.IsLarge(type)&&localMemory.Int32(address+0x12280,out var after)&&after==type)
            huntOutcomes.MonsterDied(address,type,clock.Elapsed.TotalSeconds);
    }
    private void ProcessHuntOutcome()
    {
        bool allowed=enabled&&!scenePaused&&autoCompanion&&responses.Settings.Enabled&&responses.Settings.Categories.TryGetValue("quest",out var group)&&group.Enabled;
        var result=huntOutcomes.Take(clock.Elapsed.TotalSeconds,allowed);if(result==null||!responses.IsEnabled(result.Kind))return;
        var now=clock.Elapsed.TotalSeconds;
        string text=result.Kind=="quest_complete"?"辛苦啦，任务完成！歇一歇再出发。":"讨伐成功啦，先缓口气，本鱼陪着你。";
        string fact=result.Kind=="quest_complete"?"收到原生任务完成事件，只确认任务完成，不推断回到据点、无伤、奖励或击杀者。":"收到大型怪物死亡原生事件，已只读核对World怪物类型；只说这只怪物被讨伐，不宣称主人击杀，不据此断言任务完成或捕获成功，不说全队无伤。";
        EventLine(result.Kind,"quest",text,fact);expression.Offer("happy",.75f,2,now,4);actionQuietUntil=now+2;
        WriteLog("HUNT_OUTCOME kind="+result.Kind+" source=native_event attribution=unknown");
    }
}
