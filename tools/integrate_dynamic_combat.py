"""Idempotent source migration from P11.4 to dynamic combat rules."""
from pathlib import Path
root=Path(__file__).resolve().parents[1]
for name in ['src/WhaleCompanion/WhaleCompanion.csproj','src/WhaleCompanion/PlayerDamageCounter.cs']:
    p=root/name;s=p.read_text(encoding='utf-8-sig');lines=s.splitlines();seen=set();out=[]
    for line in lines:
        unique='equipment-damage.json' in line or 'motion-values.json' in line or 'public bool DynamicThreshold' in line
        if unique and line in seen:continue
        if unique:seen.add(line)
        out.append(line)
    p.write_text('\n'.join(out)+'\n',encoding='utf-8-sig')
p=root/'src/WhaleCompanion/Class1.cs';s=p.read_text(encoding='utf-8-sig')
def replace(old,new):
    global s
    if new in s:return
    assert s.count(old)==1,(old[:80],s.count(old))
    s=s.replace(old,new)
def section(start,end,new):
    global s
    a=s.index(start);b=s.index(end,a);s=s[:a]+new+s[b:]
replace('private readonly SaedDamageWindow saedWindow=new();','private readonly CombatDamageDirector combatVerdicts=new();\n    private CombatActionRequest? combatRequest;\n    private CombatFacts? combatFacts;\n    private double nextCombatStart;')
replace('private SaedDamageResult? pendingSaedResult;','private CombatDamageResult? pendingSaedResult;')
replace('private double nextCounterPoll,nextCounterLog,nextSaedStart;','private double nextCounterPoll,nextCounterLog;')
replace('"saed-response.json"','"combat-response.json"')
replace('P11.4 owner cumulative damage and binary SAED response; experimental features OFF by default','P11.5 dynamic equipment/motion thresholds and resource-aware combat verdicts; experimental OFF')
s=s.replace('saedWindow.Reset()','combatVerdicts.Reset()').replace('saedWindow.Status','combatVerdicts.Status').replace('nextSaedStart=0','nextCombatStart=0')
replace('Experimental SAED damage verdict (good / mostly missed)','Experimental dynamic combat verdicts (14 weapons)')
replace('Reload SAED settings','Reload combat settings');replace('Save SAED settings','Save combat settings')
replace('Estimated full SAED damage (not panel attack)','Manual reference damage (used only with dynamic OFF)')
replace('Direct damage threshold (0 = estimate x fraction)','Force threshold (0 = automatic)')
replace('ImGui.TextWrapped($"Praise threshold: {saedSettings.Threshold:0.#}. Owner seat: {(saedSettings.OwnerSlot<0?"auto":(saedSettings.OwnerSlot+1).ToString())}. Edit saed-response.json then reload.");','ImGui.TextWrapped($"Threshold mode: {(saedSettings.DynamicThreshold?\"dynamic equipment + move\":\"manual reference\")}. Owner seat: {(saedSettings.OwnerSlot<0?\"auto\":(saedSettings.OwnerSlot+1).ToString())}. Config: combat-response.json");\n            bool dynamicThreshold=saedSettings.DynamicThreshold;if(ImGui.Checkbox("Dynamic equipment and move threshold",ref dynamicThreshold)){saedSettings.DynamicThreshold=dynamicThreshold;SaveSaedSettings();}')
replace('Estimated owner damage over 5s; no damage hook required. Zero-only inactive counters stay silent. AI follow-up uses paid actions switch.','Owner damage over move-specific windows. Iai also checks gauge loss. Equipment base stats scale with progression; current attack buffs/augments are not fully modeled. No new damage hook.')
replace('if(saedResponses&&sample.Weapon=="ChargeBlade"&&sample.Set==3&&sample.Id is 119 or 120){Interlocked.CompareExchange(ref pendingAction,null,sample);return;}','if(saedResponses&&CombatProfiles.Handles(sample.Weapon,sample.Set,sample.Id)){Interlocked.CompareExchange(ref pendingAction,null,sample);return;}')
replace('        // Observe only: never change the ref parameter.','''        if(saedResponses&&autoCompanion&&actionCompanion)
        {
            var actualWeapon=player.CurrentWeaponType.ToString();
            if(CombatProfiles.Handles(actualWeapon,next.ActionSet,next.ActionId))
            {
                var now=clock.Elapsed.TotalSeconds;
                var before=CombatFacts.Read(localMemory,(ulong)player.Instance,actualWeapon,weaponStateRevision,now);
                Interlocked.Exchange(ref combatRequest,new CombatActionRequest(new(actualWeapon,next.ActionSet,next.ActionId,now,(ulong)player.Instance),before));
            }
        }
        // Observe only: never change the ref parameter.''')
section('    private void PollOwnerCounter()','    private void PollNativeAction()', '''    private void PollOwnerCounter()
    {
        if(!saedResponses||!enabled||scenePaused||sampledPlayer==0||!WeaponStateProbe.Supports(weapon)||!autoCompanion||!actionCompanion)
        {ownerCounter=null;combatFacts=null;combatVerdicts.Reset();pendingSaedResult=null;Interlocked.Exchange(ref combatRequest,null);return;}
        var now=clock.Elapsed.TotalSeconds;if(now<nextCounterPoll)return;nextCounterPoll=now+.1;
        PlayerDamageCounter.TryRead(localMemory,gameModule,weaponStateRevision,saedSettings.OwnerSlot,now,out ownerCounter,out counterStatus);
        combatFacts=CombatFacts.Read(localMemory,sampledPlayer,weapon,weaponStateRevision,now);
        var result=combatVerdicts.Observe(ownerCounter,combatFacts,now);
        if(result!=null){pendingSaedResult=result;WriteLog("COMBAT_DAMAGE_VERDICT "+System.Text.Json.JsonSerializer.Serialize(result));}
        if(now>=nextCounterLog){nextCounterLog=now+5;WriteLog("OWNER_DAMAGE_COUNTER "+System.Text.Json.JsonSerializer.Serialize(new{sample=ownerCounter,status=counterStatus,window=combatVerdicts.Status,equipment=combatFacts.Equipment,fields=combatFacts.Fields}));}
    }
    private void DeliverSaedResult()
    {
        var result=pendingSaedResult;if(result==null)return;
        var now=clock.Elapsed.TotalSeconds;
        if(!saedResponses||!enabled||scenePaused||!autoCompanion||!actionCompanion||weapon!=result.Weapon||now-result.Time>5)
        {pendingSaedResult=null;return;}
        if(chat.Busy||now<actionQuietUntil)return;
        pendingSaedResult=null;var reaction=CombatDamageDirector.Reaction(result);
        LocalLine(reaction.Text);expression.Offer(reaction.Emotion,reaction.Intensity,1,now,3);actionQuietUntil=now+5;
        WriteLog($"COMBAT_VERDICT_RESPONSE kind={reaction.Kind} move={result.Key} damage={result.Damage} threshold={result.Threshold:0.#} estimated=true");
        if(aiActionEvents){var epoch=session;chat.StartEvent(reaction.Context,()=>saedResponses&&enabled&&!scenePaused&&autoCompanion&&actionCompanion&&aiActionEvents&&session==epoch&&clock.Elapsed.TotalSeconds<=now+6);}
    }
''')
a=s.index('        if(saedResponses&&autoCompanion&&actionCompanion&&value.Weapon=="ChargeBlade"')
b=s.index('        WriteLog($"NATIVE_ACTION_ENTER',a)
s=s[:a]+'''        if(saedResponses&&autoCompanion&&actionCompanion)
        {
            var request=Volatile.Read(ref combatRequest);
            if(combatVerdicts.Active||now>=nextCombatStart)
            {
                if(combatVerdicts.Enter(transition,request,ownerCounter,saedSettings))
                {
                    nextCombatStart=now+8;
                    WriteLog("COMBAT_WINDOW_START "+System.Text.Json.JsonSerializer.Serialize(new{action=value,before=request?.Before,dynamic=saedSettings.DynamicThreshold,ratio=saedSettings.PraiseRatio,manual=saedSettings.ManualThreshold}));
                    if(value.Weapon=="ChargeBlade"&&!chat.Busy&&now>=actionQuietUntil){LocalLine("要上了，本鱼看着呢！");expression.Offer("relaxed",.4f,1,now,2);actionQuietUntil=now+1;}
                }
            }
        }
'''+s[b:]
p.write_text(s,encoding='utf-8-sig')
print('Integrated dynamic combat controller; source only.')
