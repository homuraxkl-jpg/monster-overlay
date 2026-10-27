from pathlib import Path
root=Path(__file__).resolve().parents[1]
def modify(name,changes):
    p=root/name;s=p.read_text(encoding='utf-8-sig')
    for old,new in changes:
        if new in s:continue
        assert s.count(old)==1,(name,old[:65],s.count(old));s=s.replace(old,new)
    p.write_text(s,encoding='utf-8-sig' if name.endswith('Class1.cs') else 'utf-8')
modify('src/WhaleCompanion/PlayerDamageCounter.cs',[
('int Total,double Time);','int Total,double Time,string Source="party");'),
('''    static string Name(IReadOnlyLocalMemory m,ulong at)''','''    public static bool TryPartySize(IReadOnlyLocalMemory m,ulong module,string revision,out int size)
    {
        size=-1;if(revision!="421810"||!LocalProcessMemory.IsAddress(module,0x51C46C0))return false;
        var address=Chain(m,module+0x51C46B8,0x258,0x10,0x6574);
        return address!=0&&m.Int32(address,out size)&&size is >=0 and <=4;
    }
    static string Name(IReadOnlyLocalMemory m,ulong at)'''),
('public bool DynamicThreshold {get;set;}=true;','public bool DynamicThreshold {get;set;}=true;\n    public bool SoloDisplayFallback {get;set;}=true;'),
('sample.Name!=baseline.Name||sample.Total<last.Total','sample.Name!=baseline.Name||sample.Source!=baseline.Source||sample.Total<last.Total')])
modify('src/WhaleCompanion/CombatDamageRules.cs',[
('public sealed record CombatActionRequest(ActionObservation Action,CombatFacts Before);','public sealed record CombatActionRequest(ActionObservation Action,CombatFacts Before,PlayerDamageCounterSample? DamageBaseline=null);'),
('bool Good,string Mechanic,double Time);','bool Good,string Mechanic,double Time,string DamageSource="party");'),
('''        if(!window.Start(counter,current.Time,effective))return false;''','''        counter=request.DamageBaseline??counter;
        if(!window.Start(counter,current.Time,effective))return false;'''),
('result.Good&&gate,detail,now);','result.Good&&gate,detail,now,counter?.Source??"unknown");'),
('机制信息：{result.Mechanic}。这是当前装备基础数据与动作倍率的估计','机制信息：{result.Mechanic}；伤害来源={result.DamageSource}（display_local_candidate为单人显示伤害候选，不能保证排除随从或环境）。这是当前装备基础数据与动作倍率的估计')])
modify('tools/WeaponStateChecks/WeaponStateChecks.csproj',[
('<Compile Include="../../src/WhaleCompanion/CombatDamageRules.cs" Link="CombatDamageRules.cs" />','<Compile Include="../../src/WhaleCompanion/CombatDamageRules.cs" Link="CombatDamageRules.cs" />\n    <Compile Include="../../src/WhaleCompanion/DisplayedDamageCounter.cs" Link="DisplayedDamageCounter.cs" />')])
modify('src/WhaleCompanion/Class1.cs',[
('private readonly CombatFeedback combatFeedback=new();','''private readonly CombatFeedback combatFeedback=new();
    private readonly DisplayedDamageCounter displayedDamage=new();
    private bool combatFallbackObserverOn;
    private double nextFallbackAttempt;'''),
('''        PollOwnerCounter();
        PollNativeAction();
        DeliverSaedResult();
        ProcessDamageObservations();''','''        ProcessDamageObservations();
        PollOwnerCounter();
        PollNativeAction();
        DeliverSaedResult();'''),
('new CombatActionRequest(new(actualWeapon,next.ActionSet,next.ActionId,now,(ulong)player.Instance),before)','new CombatActionRequest(new(actualWeapon,next.ActionSet,next.ActionId,now,(ulong)player.Instance),before,ownerCounter)'),
('else {damageObserver?.Stop();damageStatus="Damage observer OFF";}','else {if(!combatFallbackObserverOn)damageObserver?.Stop();damageStatus=combatFallbackObserverOn?"Solo display fallback active":"Damage observer OFF";}'),
('''        {ownerCounter=null;combatFacts=null;combatVerdicts.Reset();pendingSaedResult=null;Interlocked.Exchange(ref combatRequest,null);return;}''','''        {StopCombatFallback();ownerCounter=null;combatFacts=null;combatVerdicts.Reset();pendingSaedResult=null;Interlocked.Exchange(ref combatRequest,null);return;}'''),
('''        combatFacts=CombatFacts.Read(localMemory,sampledPlayer,weapon,weaponStateRevision,now);''','''        bool solo=PlayerDamageCounter.TryPartySize(localMemory,gameModule,weaponStateRevision,out var partySize)&&partySize==0;
        if(saedSettings.SoloDisplayFallback&&solo&&(ownerCounter==null||ownerCounter.Total==0||combatVerdicts.Active&&combatFallbackObserverOn))
        {
            if(!combatFallbackObserverOn&&now>=nextFallbackAttempt)
            {
                nextFallbackAttempt=now+30;damageObserver??=new(damageBuffer,()=>clock.Elapsed.TotalSeconds,()=>Volatile.Read(ref session));
                combatFallbackObserverOn=damageObserver.Start(weaponStateRevision);
                if(combatFallbackObserverOn){displayedDamage.Reset(session,sampledPlayer,now);lastDamageDropped=damageBuffer.Dropped;damageBuffer.Clear();}
                WriteLog("SOLO_DAMAGE_FALLBACK "+damageObserver.Status);
            }
            if(combatFallbackObserverOn)
            {
                if(!displayedDamage.Matches(session,sampledPlayer)){displayedDamage.Reset(session,sampledPlayer,now);damageBuffer.Clear();lastDamageDropped=damageBuffer.Dropped;}
                ownerCounter=displayedDamage.Sample(now);counterStatus=ownerCounter==null?"Display fallback capture gap; restart probe":"Solo displayed damage candidate: "+ownerCounter.Total;
            }
            else counterStatus+="; "+(damageObserver?.Status??"fallback unavailable");
        }
        else StopCombatFallback();
        combatFacts=CombatFacts.Read(localMemory,sampledPlayer,weapon,weaponStateRevision,now);'''),
('''    private void DeliverSaedResult()''','''    private void StopCombatFallback()
    {
        if(!combatFallbackObserverOn)return;
        combatFallbackObserverOn=false;displayedDamage.Reset(-1,0,clock.Elapsed.TotalSeconds);
        if(!damageDiagnostic)damageObserver?.Stop();
    }
    private void DeliverSaedResult()'''),
('''        if(!damageDiagnostic){damageBuffer.Clear();return;}''','''        if(!damageDiagnostic&&!combatFallbackObserverOn){damageBuffer.Clear();return;}'''),
('''if(captureGap){lastDamageDropped=damageBuffer.Dropped;damageAttribution.MarkGap();}''','''if(captureGap){lastDamageDropped=damageBuffer.Dropped;damageAttribution.MarkGap();displayedDamage.Gap();}'''),
('''if(fact is AttackObservation a){if(a.Epoch==session&&enabled&&!scenePaused)damageAttribution.Action(a);}''','''if(fact is AttackObservation a){if(damageDiagnostic&&a.Epoch==session&&enabled&&!scenePaused)damageAttribution.Action(a);}'''),
('''{damageAttribution.Damage(d);if(damageLogBudget-->0)WriteLog("DAMAGE_RAW "+System.Text.Json.JsonSerializer.Serialize(d));else damageLogSuppressed++;}''','''{
                if(combatFallbackObserverOn)displayedDamage.Add(d);
                if(damageDiagnostic){damageAttribution.Damage(d);if(damageLogBudget-->0)WriteLog("DAMAGE_RAW "+System.Text.Json.JsonSerializer.Serialize(d));else damageLogSuppressed++;}
            }'''),
('''if(captureGap||damageBuffer.Dropped!=lastDamageDropped){lastDamageDropped=damageBuffer.Dropped;damageAttribution.MarkGap();}''','''if(captureGap||damageBuffer.Dropped!=lastDamageDropped){lastDamageDropped=damageBuffer.Dropped;damageAttribution.MarkGap();displayedDamage.Gap();}'''),
('''        foreach(var result in damageAttribution.Finish(session,now))WriteLog''','''        if(!damageDiagnostic)return;
        foreach(var result in damageAttribution.Finish(session,now))WriteLog''')])
print('Integrated single-player/loaded-scene-only display fallback; source remains explicit candidate.')
