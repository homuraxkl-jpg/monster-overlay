from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'src/WhaleCompanion/Class1.cs'
s=p.read_text(encoding='utf-8-sig')
def edit(a,b):
    global s
    assert s.count(a)==1,(a[:80],s.count(a))
    s=s.replace(a,b)
edit('private readonly CombatDamageDirector combatVerdicts=new();','private readonly CombatDamageDirector combatVerdicts=new();\n    private readonly TenderizeTracker tenderizeTracker=new();\n    private double nextTenderizePoll;')
edit('''        PollNativeAction();
        DeliverSaedResult();''','''        PollNativeAction();
        PollTenderize();
        DeliverSaedResult();''')
edit('''    private void ProcessGuardRecoil()''','''    private void PollTenderize()
    {
        if(!enabled||scenePaused||sampledPlayer==0||!saedResponses){tenderizeTracker.Reset();return;}
        double now=clock.Elapsed.TotalSeconds;if(now<nextTenderizePoll)return;nextTenderizePoll=now+.25;
        try
        {
            var samples=Monster.GetAllMonsters().Take(16).SelectMany(monster=>TenderizeProbe.Read(localMemory,(ulong)monster.Instance,weaponStateRevision,now));
            foreach(var change in tenderizeTracker.Observe(samples,now))
                WriteLog("TENDERIZE_TRANSITION "+System.Text.Json.JsonSerializer.Serialize(new{change,owner="unknown",validated=false,ai_response=false}));
        }
        catch(Exception ex){tenderizeTracker.Reset();nextTenderizePoll=now+10;WriteLog("TENDERIZE_READ_FAILED "+ex.GetType().Name);}
    }
    private void ProcessGuardRecoil()''')
edit('''public void OnUnload() { damageObserver''','''public void OnUnload() { WriteLog("PLUGIN_UNLOAD_BEGIN");damageObserver''')
edit('''P11.5 dynamic equipment/motion thresholds and resource-aware combat verdicts; experimental OFF''','''P11.5 feedback revision 2: phase-isolated damage, CB final branch, hammer FESTA7, foresight branch; tenderize observation only''')
p.write_text(s,encoding='utf-8')
p=root/'tools/WeaponStateChecks/WeaponStateChecks.csproj'
s=p.read_text(encoding='utf-8-sig').replace('</Project>','<ItemGroup><Compile Include="../../src/WhaleCompanion/TenderizeProbe.cs" Link="TenderizeProbe.cs" /></ItemGroup></Project>')
p.write_text(s,encoding='utf-8')
print('Integrated read-only tenderize result observation; no owner claim or AI trigger.')
