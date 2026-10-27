using WhaleCompanion;
try
{
int checks=0;
void Check(bool ok,string label){if(!ok)throw new Exception(label);checks++;}
const ulong player=0x100000,data=0x200000;
MemoryFixture Fixture(){var m=new MemoryFixture();m.Pointers[player+0x76B0]=data;return m;}
bool Read(MemoryFixture m,string w,out WeaponStateSnapshot? s){m.Values[data+0x9F8]=Array.IndexOf(WeaponStateProbe.Weapons,w);return WeaponStateProbe.TryRead(m,player,w,"421810",out s,out _);}
var memory=Fixture();
Check(!WeaponStateProbe.TryRead(memory,player,"DualBlades","unknown",out _,out _)&&memory.Reads==0,"unknown version does not read");
Check(!Read(memory,"Unsupported",out _)&&memory.Reads==0,"unsupported weapon does not read");
Check(!Read(memory,"DualBlades",out _),"missing bytes not false zero state");
memory.Values[data+0x2368]=0x101;memory.Float(data+0x236C,.75f);
Check(Read(memory,"DualBlades",out var db)&&db!.Fields["demon_mode"]==1&&db.Fields["arch_demon_mode"]==1&&db.Fields["demon_gauge_raw"]==.75,"packed bools and gauge");
memory.Values[data+0x2368]=0x102;Check(!Read(memory,"DualBlades",out _),"invalid boolean rejected");
memory.Values[data+0x2368]=1;
foreach(var bad in new[]{float.NaN,float.PositiveInfinity,-1f,2f}){memory.Float(data+0x236C,bad);Check(!Read(memory,"DualBlades",out _),"invalid gauge rejected");}
memory=Fixture();foreach(uint offset in new uint[]{0x2370,0x2378,0x237C,0x2470})memory.Float(data+offset,0);
memory.Values[data+0x2374]=6;Check(Read(memory,"ChargeBlade",out var cb)&&cb!.Fields["phials"]==6&&!cb.Fields.ContainsKey("full_phials"),"six phials not assumed full");
memory.Values[data+0x2374]=7;Check(!Read(memory,"ChargeBlade",out _),"invalid phials rejected");
memory=Fixture();foreach(uint offset in new uint[]{0x2350,0x2360,0x2364})memory.Float(data+offset,0);
Check(Read(memory,"SwitchAxe",out var sa)&&!sa!.Fields.ContainsKey("power_axe"),"power axe not inferred from amp");
memory=Fixture();foreach(uint offset in new uint[]{0x2368,0x236C,0x2370})memory.Float(data+offset,120);
Check(Read(memory,"InsectGlaive",out var ig)&&ig!.Fields.Count==3,"three extract timers");
memory.ChangePointer=true;Check(!Read(memory,"InsectGlaive",out _),"weapon pointer race rejected");
var tracker=new WeaponStateTracker();tracker.Observe(ig,0);tracker.Observe(ig,0);Check(!tracker.Ready,"same timestamp is not another sample");tracker.Observe(ig,.1);tracker.Observe(ig,.2);Check(tracker.Ready,"three fresh samples ready");
tracker.Observe(ig,2);Check(!tracker.Ready,"gap resets readiness");tracker.Observe(ig,2.1);tracker.Observe(ig,2.2);Check(tracker.Ready,"recovery from gap");
tracker.Observe(ig! with{Owner=player+8},2.3);Check(!tracker.Ready,"owner change resets");
tracker.Observe(null,2.4);Check(tracker.Current==null&&!tracker.Ready,"unreadable clears stale data");
// Populate a separate legacy object; all offset translation must stay off the SPL object.
MemoryFixture Legacy()
{
    var m=Fixture();ulong current=player,next=0x300000;
    foreach(uint offset in new uint[]{0x98,0x10,0x70,0x18,0x550}){m.Pointers[current+offset]=next;current=next;next+=0x10000;}
    for(uint i=0;i<0x2900;i+=4)m.Values[current+i]=0;
    m.Values[current+0xAA1]=1;
    for(uint i=0x2350;i<0x2490;i+=4)m.Values[data+i]=0;
    m.Pointers[current+0x2450]=0;m.Pointers[current+0x2470]=0;
    return m;
}
foreach(var w in WeaponStateProbe.Weapons)
{
    var m=Legacy();Check(Read(m,w,out var state)&&state!.Weapon==w,"all fourteen entries: "+w);
}
var independent=Legacy();independent.Float(0x340000+0x2358,2.5f);independent.Values[0x340000+0x235C]=3;
Check(Read(independent,"Hammer",out var hammer)&&hammer!.Fields["charge_level_raw"]==3&&hammer.Fields["charge_progress_raw"]==2.5,"legacy chain independent from current weapon");
independent.Values[data+0x9F8]=9;Check(!WeaponStateProbe.TryRead(independent,player,"Hammer","421810",out _,out _),"mismatched weapon object rejected");
var capacity=Legacy();capacity.Pointers[player+0x7D20]=0x400000;capacity.Pointers[0x400010]=0x500000;capacity.Values[0x500000+0x78+69*24+8]=1;capacity.Values[data+0x2374]=6;
Check(Read(capacity,"ChargeBlade",out var six)&&six!.Fields["max_phials"]==6&&six.Fields["full_phials"]==1,"capacity skill yields six full phials");
capacity.Values[0x500000+0x78+69*24+8]=0;capacity.Values[data+0x2374]=5;
Check(Read(capacity,"ChargeBlade",out var five)&&five!.Fields["max_phials"]==5&&five.Fields["full_phials"]==1,"no capacity skill yields five full phials");
var axe=Legacy();axe.Pointers[player+0x7D20]=0x400000;axe.Values[0x4006E5]=1;axe.Float(0x4006E8,30);
Check(Read(axe,"SwitchAxe",out var power)&&power!.Fields["power_axe"]==1&&power.Fields["amp_timer_raw"]==0,"axe buff independent from sword amp");
var stateDirector=new WeaponStateCompanion();
var s=new WeaponStateSnapshot("Hammer",player,data,new(){["power_charge"]=0});
for(int i=0;i<3;i++)Check(stateDirector.Observe(s,i*.1)==null,"initial state silent");
s=s with{Fields=new(){["power_charge"]=1}};
Check(stateDirector.Observe(s,.3)==null&&stateDirector.Observe(s,.4)==null,"state edge debounced");
Check(stateDirector.Observe(s,.5)?.Kind=="weapon_state_power_charge","power charge response");
Check(stateDirector.Observe(s,.6)==null,"held buff does not repeat");
var allActions=new ActionCompanion{IncludeAllWeapons=true};
foreach(var (w,id) in new[]{("Hammer",81),("ChargeBlade",120),("SwitchAxe",125)})
{allActions.Reset();var response=allActions.Observe(new(w,3,id,1),1);Check(response!=null&&response.Context.Contains("不知道是否命中"),"priority weapon action conservative: "+w);}
foreach(var w in WeaponStateProbe.Weapons)Check(Enumerable.Range(0,200).Any(id=>WeaponActions.Name(w,3,id)!=null),"all weapon action catalog: "+w);
Check(WeaponActions.Name("Hammer",4,81)==null,"action sets not interchangeable");
var attribution=new DamageAttribution();
AttackObservation Attack(int id,double time,int epoch=1)=>new(epoch,player,new("ChargeBlade",3,id,time));
attribution.Action(Attack(120,0));attribution.Damage(new(1,.5,0x500000,100,44));
var result=attribution.Finish(1,6).Single();Check(result.Verdict=="temporal_candidate"&&result.ConfirmedCallbacks==0&&result.ObservedDamage==100,"damage timing is not ownership");
attribution.Reset();attribution.Action(Attack(120,0));Check(attribution.Finish(1,6).Single().Verdict=="unknown_no_damage","no damage is not miss");
attribution.Reset();attribution.Action(Attack(120,0));attribution.Action(Attack(118,.2));attribution.Damage(new(1,.5,0x500000,100,44));Check(attribution.Ambiguous==1&&attribution.Finish(1,6).All(x=>x.Verdict=="ambiguous"),"overlapping delayed attacks not guessed");
attribution.Reset();attribution.BindValidatedAttack("ChargeBlade",120,44);attribution.Action(Attack(120,0));attribution.Damage(new(1,.5,0x500000,100,44,VerifiedSourceOwner:player));Check(attribution.Finish(1,6).Single().Verdict=="confirmed_hit","validated owner AND attack binding required");
attribution.Reset();attribution.Action(Attack(120,0));attribution.Damage(new(1,.5,0x500000,100,45,VerifiedSourceOwner:player));Check(attribution.Finish(1,6).Single().Verdict=="temporal_candidate","owner alone not enough for attack identity");
attribution.Reset();attribution.Action(Attack(120,0));attribution.Damage(new(1,.5,0x500000,100,44,VerifiedSourceOwner:player+8));Check(attribution.Finish(1,6).Single().ConfirmedCallbacks==0,"team damage cannot become owner damage");
attribution.Reset();attribution.Action(Attack(120,0));attribution.MarkGap();attribution.Damage(new(1,.5,0x500000,100,44,VerifiedSourceOwner:player));Check(attribution.Finish(1,6).Single().Verdict=="coverage_gap","overflow cannot confirm hit");
attribution.Reset();attribution.Action(Attack(120,0));attribution.Damage(new(2,.5,0x500000,100,44));Check(attribution.Unattributed==1&&attribution.Finish(2,1).Single().Verdict=="scene_changed","scene epoch isolates old attacks");
attribution.Reset();attribution.Action(Attack(120,0));attribution.Damage(new(1,2,0x500000,100,44));attribution.Damage(new(1,2,0x500000,100,44));Check(attribution.Finish(1,6).Single().ObservedCallbacks==2,"equal simultaneous values can be distinct callbacks");
var inbox=new DamageCaptureBuffer();for(int i=0;i<600;i++)inbox.Add(new DamageObservation(1,i,1,1,1));Check(inbox.Dropped==88&&inbox.Drain().Length==512,"bounded capture buffer");
attribution.Reset();attribution.Action(Attack(119,0));attribution.Action(Attack(120,.2));attribution.Damage(new(1,.5,0x500000,100,44));var stages=attribution.Finish(1,6).Single();Check(stages.Stages.SequenceEqual(new[]{119,120})&&stages.Verdict=="temporal_candidate","super discharge phases form one instance");
attribution.Reset();foreach(var (id,time) in new[]{(87,0d),(89,.2),(90,.4),(91,.6),(92,.8)})attribution.Action(new(1,player,new("LongSword",3,id,time)));attribution.Damage(new(1,1,0x500000,100,44));Check(attribution.Finish(1,6).Single().Stages.Length==5,"helm breaker phases form one instance");
var guardMemory=Fixture();guardMemory.Pointers[player+0x7D20]=0x400000;guardMemory.Float(0x40079C,12);
Check(!NativeGuardSignal.TryRead(guardMemory,player,"ChargeBlade","unknown",out _)&&guardMemory.Reads==0,"native guard rejects unknown version before reading");
Check(!NativeGuardSignal.TryRead(guardMemory,player,"Bow","421810",out _)&&guardMemory.Reads==0,"bow has no fabricated guard signal");
Check(NativeGuardSignal.TryRead(guardMemory,player,"ChargeBlade","421810",out var nativeGuard)&&nativeGuard!.Timer==12,"native skill timer independent of weapon mechanics chain");
guardMemory.Float(0x40079C,float.NaN);Check(!NativeGuardSignal.TryRead(guardMemory,player,"ChargeBlade","421810",out _),"native guard invalid field stays unknown");
var guard=new NativeGuardCompanion();
for(int i=0;i<3;i++)Check(guard.Observe(nativeGuard,i*.1)==null,"already active guard is silent baseline");
guard.Reset();var inactive=nativeGuard! with{Timer=0};for(int i=0;i<3;i++)Check(guard.Observe(inactive,i*.1)==null,"inactive guard is not failed GP");
Check(guard.Observe(nativeGuard,.3)==null&&guard.Observe(nativeGuard,.4)==null,"native guard debounced");
var guardReply=guard.Observe(nativeGuard,.5);Check(guardReply?.Kind=="offensive_guard_activated"&&guardReply.Context.Contains("不能说确定盾斧GP"),"offensive guard activation is not GP assertion");
for(int i=6;i<=156;i++)guard.Observe(nativeGuard! with{Timer=2},i*.1);
Check(guard.Observe(nativeGuard,15.7)==null&&guard.Observe(nativeGuard! with{Timer=11.9f},15.8)==null,"guard timer refresh waits for samples");
Check(guard.Observe(nativeGuard! with{Timer=11.8f},15.9)?.Kind=="offensive_guard_refresh","active native guard refresh can be observed");
Check(guard.Observe(null,16)==null,"missing native guard evidence is silent");
for(int i=0;i<3;i++)Check(guard.Observe(nativeGuard! with{Owner=player+8},17+i*.1)==null,"new owner guard baseline cannot inherit success");
Check(WeaponActions.Identify("Bow",3,91)==null,"ordinary bow coating does not trigger praise");
var am=Fixture();am.Pointers[player+0x62C8]=player;am.Values[player+0x6274]=3;am.Values[player+0x6278]=70;
am.Values[data+0x9F8]=9;
Check(!NativeActionSignal.TryRead(am,player,"ChargeBlade","unknown",1,out _)&&am.Reads==0,"action version gate before memory access");
Check(NativeActionSignal.TryRead(am,player,"ChargeBlade","421810",1,out var actual)&&actual!.Id==70,"read actual controller action");
am.Pointers[player+0x62C8]=player+8;Check(!NativeActionSignal.TryRead(am,player,"ChargeBlade","421810",1,out _),"foreign controller rejected");am.Pointers[player+0x62C8]=player;
am.Values[player+0x6274]=4;Check(!NativeActionSignal.TryRead(am,player,"ChargeBlade","421810",1,out _),"invalid action set rejected");am.Values[player+0x6274]=3;
am.Values[player+0x6278]=-1;Check(!NativeActionSignal.TryRead(am,player,"ChargeBlade","421810",1,out _),"invalid action id rejected");am.Values[player+0x6278]=70;
am.Values[data+0x9F8]=4;Check(!NativeActionSignal.TryRead(am,player,"ChargeBlade","421810",1,out _),"action weapon mismatch rejected");am.Values[data+0x9F8]=9;
am.ChangePointer=true;Check(!NativeActionSignal.TryRead(am,player,"ChargeBlade","421810",1,out _),"changing controller pointer rejected");
var actions=new NativeActionTracker();
Check(actions.Observe(actual)==null,"initial active action not invented as new entry");
Check(actions.Observe(actual! with{Time=1.1})==null,"held action is not another attack");
var entered=actions.Observe(actual! with{Id=35,Time=1.2});Check(entered?.Previous.Id==70&&entered.Current.Id==35,"observed action transition");
var requested=new ActionObservation("ChargeBlade",3,35,1.15,player);
Check(NativeActionSignal.Confirms(requested,entered,1.21),"actual entry confirms earlier matching request");
Check(!NativeActionSignal.Confirms(requested,null,1.21),"request alone not confirmed");
Check(!NativeActionSignal.Confirms(requested with{Owner=player+8},entered,1.21),"another owner cannot confirm request");
Check(!NativeActionSignal.Confirms(requested with{Time=1.21},entered,1.22),"same id request cannot reuse earlier transition");
Check(!NativeActionSignal.Confirms(requested,entered,1.6),"stale actual action not confirmation");
Check(actions.Observe(actual! with{Id=35,Time=1.2})==null,"duplicate timestamp no transition");
Check(actions.Observe(actual! with{Id=36,Time=2})==null&&actions.LatestTransition==null,"observation gap restarts baseline");
Check(actions.Observe(actual! with{Owner=player+8,Time=2.1})==null,"owner swap baseline silent");
actions.Observe(null);Check(actions.Current==null&&actions.LatestTransition==null,"unreadable current action clears history");
var recoil=NativeGuardRecoil.Identify(entered!);
Check(recoil?.GpActionCandidate==true&&NativeGuardRecoil.Reaction(recoil).Context.Contains("不知道攻击来源、精确GP"),"GP capable preceding action remains unproven");
foreach(int id in Enumerable.Range(35,6))Check(NativeGuardRecoil.Identify(entered! with{Current=entered.Current with{Id=id}})!=null,"guard recoil catalog "+id);
Check(NativeGuardRecoil.Identify(entered! with{Current=entered.Current with{Id=102}})==null,"guard stance not defensive impact");
Check(NativeGuardRecoil.Identify(new(actual! with{Weapon="Hammer"},actual! with{Weapon="Hammer",Id=35,Time=1.2}))==null,"non shield weapon not defensive impact");
Check(NativeGuardRecoil.Identify(entered! with{Previous=entered.Previous with{Id=102}})?.GpActionCandidate==false,"normal guard not GP candidate");
// Real World map semantics: dereference THEN add, final entry is not dereferenced.
const ulong module=0x140000000;
var cm=new MemoryFixture();ulong allocation=0x800000;
ulong CounterChain(ulong root,params uint[] offsets){foreach(var offset in offsets){cm.Pointers[root]=allocation;root=allocation+offset;allocation+=0x10000;}return root;}
void PutName(ulong at,string text){var bytes=new byte[32];System.Text.Encoding.UTF8.GetBytes(text).CopyTo(bytes,0);for(int i=0;i<8;i++)cm.Values[at+(uint)i*4]=System.Buffers.Binary.BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(i*4,4));}
var damageAddress=CounterChain(module+0x51C46B8,0x258,0x38,0x450,0x8,0x48);
var partyAddress=CounterChain(module+0x5013530,0x1AB0);
var saveAddress=CounterChain(module+0x5013950,0xA8);
cm.Values[saveAddress+0x44]=0;cm.Pointers[saveAddress]=0xA00000;PutName(0xA00050,"猎人");
cm.Pointers[partyAddress+0x58]=0xB00000;PutName(0xB00049,"猎人");cm.Values[damageAddress+0x2A0]=1000;
Check(!PlayerDamageCounter.TryRead(cm,module,"other",-1,1,out _,out _)&&cm.Reads==0,"counter rejects unknown revision before reads");
Check(PlayerDamageCounter.TryRead(cm,module,"421810",-1,1,out var owner,out _)&&owner!.Slot==1&&owner.Total==1000,"auto owner matches unique UTF8 save name and seat stride");
cm.Pointers[partyAddress+0x58*2]=0xB10000;PutName(0xB10049,"猎人");cm.Values[damageAddress+0x2A0*2]=9999;
Check(!PlayerDamageCounter.TryRead(cm,module,"421810",-1,1,out _,out _),"duplicate owner names require selection");
Check(PlayerDamageCounter.TryRead(cm,module,"421810",1,1,out owner,out _)&&owner!.Total==1000,"manual owner seat does not mix teammate damage");
Check(!PlayerDamageCounter.TryRead(cm,module,"421810",4,1,out _,out _),"invalid owner seat rejected");
cm.Values[damageAddress+0x2A0]=-1;Check(!PlayerDamageCounter.TryRead(cm,module,"421810",1,1,out _,out _),"negative cumulative damage rejected");cm.Values[damageAddress+0x2A0]=1000;
var settings=new SaedResponseSettings();Check(settings.Valid&&Math.Abs(settings.Threshold-400)<.001,"default estimated reference 1200 divided by three");
settings.ManualThreshold=600;Check(settings.Threshold==600,"explicit threshold overrides reference");settings.ManualThreshold=0;
Check(!new SaedResponseSettings{PraiseRatio=double.NaN}.Valid&&!new SaedResponseSettings{WindowSeconds=0}.Valid,"invalid settings rejected");
var window=new SaedDamageWindow();var baseCounter=owner! with{Time=0,Total=1000};
Check(!window.Start(null,0,settings)&&!window.Start(baseCounter,1,settings),"missing or stale baseline cannot start verdict");
Check(window.Start(baseCounter,0,settings)&&!window.Start(baseCounter,0,settings),"stage chain starts one damage window");
SaedDamageResult? verdict=null;for(int i=1;i<=50;i++)verdict=window.Observe(baseCounter with{Time=i*.1,Total=i>=40?1400:1000},i*.1)??verdict;
Check(verdict?.Good==true&&verdict.Damage==400,"late phial damage counted and equality praises");
Check(window.Observe(baseCounter with{Time=5.1,Total=1500},5.1)==null,"one verdict per discharge");
window.Start(baseCounter,0,settings);for(int i=1;i<=50;i++)verdict=window.Observe(baseCounter with{Time=i*.1,Total=1000},i*.1)??(i==1?null:verdict);
Check(verdict?.Good==false&&verdict.Damage==0,"live positive counter with no increase gives binary teasing");
window.Start(baseCounter with{Total=0},0,settings);verdict=null;for(int i=1;i<=50;i++)verdict=window.Observe(baseCounter with{Time=i*.1,Total=0},i*.1)??verdict;
Check(verdict==null&&!window.Active,"zero-only inactive area not treated as miss");
window.Start(baseCounter,0,settings);Check(window.Observe(baseCounter with{Time=.1,Total=0},.1)==null&&!window.Active,"counter reset cancels verdict");
window.Start(baseCounter,0,settings);Check(window.Observe(baseCounter with{Time=1},1)==null&&!window.Active,"counter sampling gap cancels verdict");
window.Start(baseCounter,0,settings);Check(window.Observe(baseCounter with{Time=.1,Member=baseCounter.Member+8},.1)==null&&!window.Active,"replaced party member cancels verdict");
window.Start(baseCounter,0,settings);Check(window.Observe(null,.1)==null&&!window.Active,"read failure cancels verdict instead of mock");
window.Start(baseCounter,0,settings);Check(window.Observe(baseCounter with{Time=.1,Counter=baseCounter.Counter+8},.1)==null&&!window.Active,"changed quest counter cancels verdict");
Check(SaedDamageWindow.Reaction(new(400,400,true,5)).Kind=="saed_good"&&SaedDamageWindow.Reaction(new(0,400,false,5)).Kind=="saed_mostly_missed","exactly two response kinds");
CombatRulesChecks.Run(Check);
CombatExtendedChecks.Run(Check);
SoloFallbackChecks.Run(Check);
CombatFeedbackRegressionChecks.Run(Check);
TenderizeChecks.Run(Check);
DefensiveReactionChecks.Run(Check);
var evasionMemory=new MemoryFixture(); evasionMemory.Pointers[0x112608]=0x200000; evasionMemory.Float(0x200FC4,12);
Check(NativeEvasionSignal.TryRead(evasionMemory,0x100000,"421810",out var evasionTime)&&evasionTime==12,"mantle timer reads FC4 independently of weapon");
Check(!NativeEvasionSignal.TryRead(evasionMemory,0x100000,"unknown",out _),"mantle timer rejects unknown revision");
foreach(var gun in new[]{"LightBowgun","HeavyBowgun"})foreach(var shot in new[]{68,70,72,73,74,75,76})Check(WeaponActions.Identify(gun,3,shot)==null,"ordinary bowgun fire has neither damage nor generic chatter");
var hornChatter=new ActionCompanion{IncludeAllWeapons=true};
hornChatter.Observe(new("HeavyBowgun",3,118,0,player,true),0);
Check(hornChatter.Observe(new("HuntingHorn",3,74,1,player,true),1)?.Kind=="horn_spin","horn spin bypasses other preparation cooldown");
Check(hornChatter.Observe(new("HuntingHorn",3,74,2,player,true),2)==null,"horn spin multihits do not chatter");
Check(hornChatter.Observe(new("HuntingHorn",3,74,19,player,true),19)?.Kind=="horn_spin","horn spin own cooldown expires");
Check(WeaponActions.AllowsAlongsideDamage("HuntingHorn",3,74)&&!WeaponActions.AllowsAlongsideDamage("LongSword",3,102),"only requested horn accompaniment bypasses damage chatter suppression");
var scopeChatter=new ActionCompanion{IncludeAllWeapons=true};
Check(scopeChatter.Observe(new("HeavyBowgun",3,118,0,player,true),0)?.Context.Contains("不能确认镜头已经开启")==true,"scope change does not claim enabled or ready aim");
var lanceChatter=new ActionCompanion{IncludeAllWeapons=true};
Check(lanceChatter.Observe(new("Lance",3,76,0,player,true),0)?.Context.Contains("不说已挡住攻击")==true,"lance preparation never claims counter success");
var lanceDamage=CombatDamageDirector.Reaction(new("Lance","lance_counter","反击突刺",80,100,100,false,"未确认防御",0,Outcome:"lance_counter_damage"));
Check(lanceDamage.Kind=="lance_counter_damage"&&lanceDamage.Context.Contains("不说挡住攻击"),"lance damage response cannot claim successful guard");
foreach(var outcome in new[]{"ig_dive_damage","ig_dive_follow"})
{
 var igLow=CombatDamageDirector.Reaction(new("InsectGlaive","ig_dive","急袭突刺",1,100,50,false,"",0,Outcome:outcome));
 var igZero=CombatDamageDirector.Reaction(new("InsectGlaive","ig_dive","急袭突刺",0,100,50,false,"",0,Outcome:outcome));
 Check(igLow.Text==igZero.Text,"low and zero damage share the negative tier without intermediate evaluation");
 Check(igZero.Context.Contains("正面或负面两档"),"dive AI context requires binary evaluation");
 Check(igLow.Context.Contains(outcome=="ig_dive_follow"?"不确认飞圆命中":"不编造后续连招"),"dive reaction preserves follow-up evidence boundary");
}
Check(CombatDamageDirector.NeedsEveryEntry("Bow",3,75)&&!CombatDamageDirector.NeedsEveryEntry("Bow",3,78),"power shot counting bypasses generic eight-second start throttle only for power shot");
var powerDirector=new CombatDamageDirector();
var powerSettings=new SaedResponseSettings{DynamicThreshold=false,ReferenceDamage=100};
bool Power(double time,ulong equipment=2)
{
 var facts=new CombatFacts(player,"Bow",new(player,equipment,new("Bow",0,100,0,0,0),1,time),new(),time);
 var current=new NativeActionSample(player,"Bow",3,75,time);
 return powerDirector.Enter(new(current with{Id=0},current),new(new("Bow",3,75,time,player,true),facts,baseCounter with{Time=time}),baseCounter with{Time=time},powerSettings);
}
Check(!Power(0)&&!Power(0)&&!Power(4)&&Power(8),"three distinct power shots inside inclusive eight seconds start one window; duplicate entry ignored");
powerDirector.Reset();
Check(!Power(0)&&!Power(4)&&!Power(8.01)&&Power(9),"expired shot falls out of sliding window");
powerDirector.Reset();Power(0);Power(1);
powerDirector.Observe(baseCounter with{Time=2},new(player,"LongSword",null,new(),2),2);
Check(!Power(3),"switching away and back clears partial power-shot batch");
powerDirector.Reset();Power(0);Power(1);
Check(!Power(2,3)&&!Power(3,3)&&Power(4,3),"equipment switch clears partial power-shot batch");
CombatDamageResult? Spine(bool finish,bool land,int damage=300,bool midStart=false,bool noCounter=false)
{
 var director=new CombatDamageDirector();var settings=new SaedResponseSettings{DynamicThreshold=false,ReferenceDamage=100};
 CombatFacts Facts(double t)=>new(player,"DualBlades",null,new(),t);
 var first=new NativeActionSample(player,"DualBlades",3,midStart?127:126,0);
 bool started=director.Enter(new(first with{Id=0},first),new(new("DualBlades",3,first.Id,0,player,true),Facts(0),baseCounter),noCounter?null:baseCounter,settings);
 if(midStart){Check(!started,"spine finisher cannot create whole combo verdict");return null;}
 Check(director.Active,"spine starts at confirmed run entry without starting damage window");
 var previous=first;
 for(int i=1;i<=60;i++)Check(director.Observe(noCounter?null:baseCounter with{Time=i*.1,Total=baseCounter.Total+damage},Facts(i*.1),i*.1)==null,"spine repeated hit ticks wait for action completion");
 void Next(int id,double t){var current=first with{Id=id,Time=t};director.Enter(new(previous,current),null,noCounter?null:baseCounter with{Time=t,Total=baseCounter.Total+damage},settings);previous=current;}
 if(finish)Next(127,6.1);
 if(land)Next(128,6.2);
 Next(0,6.3);
 var result=director.Observe(baseCounter with{Time=6.3,Total=baseCounter.Total+damage+1000},Facts(6.3),6.3);
 Check(director.Observe(baseCounter with{Time=6.4,Total=baseCounter.Total+damage+2000},Facts(6.4),6.4)==null,"spine one verdict per combo");
 return result;
}
Check(Spine(true,true) is {Good:true,Damage:0,DamageSource:"action_only"},"ordered spine phases and damage permit one positive verdict excluding follow-up");
Check(Spine(false,true)==null,"spine landing without finisher cannot praise full combo");
Check(Spine(true,false)==null,"spine finisher without landing cannot praise full combo");
Check(Spine(true,true,1) is {Good:true},"complete spine ignores low damage");
Check(Spine(true,true,0) is {Good:true},"complete spine ignores zero damage");
Spine(true,true,midStart:true);
Check(Spine(true,true,noCounter:true) is {Good:true,DamageSource:"action_only"},"spine sequence works without damage counter");
Check(NativeGuardRecoil.Identify(new(new(player,"Lance",3,76,0),new(player,"Lance",3,82,.1))) is {RecoilId:82,Severity:"large",GpActionCandidate:false},"lance counter large recoil tracked separately from preparation and damage");
Check(NativeGuardRecoil.Identify(new(new(player,"Lance",3,0,0),new(player,"Lance",3,82,.1)))==null,"isolated lance counter guard does not invent preparation linkage");
var hornEffects=new HornEffectCompanion();
WeaponStateSnapshot Song(double timer,ulong who=player)=>new("HuntingHorn",who,0x200000,new(){["song_effect_3C_raw"]=timer});
for(int i=0;i<3;i++)Check(hornEffects.Observe(Song(30-i),i*.1)==null,"initial active horn timer stays silent");
for(int i=3;i<=205;i++)hornEffects.Observe(Song(10),i*.1);
hornEffects.Observe(Song(60),20.6);hornEffects.Observe(Song(59),20.7);
var refreshed=hornEffects.Observe(Song(58),20.8);
Check(refreshed?.Kind=="horn_effect_refreshed"&&refreshed.Text.Contains("攻击力提升")&&refreshed.Context.Contains("可能来自队友"),"stable refresh names actual effect without musician attribution");
Check(hornEffects.Observe(Song(57),20.9)==null,"normal timer decrease does not repeat");
Check(hornEffects.Observe(Song(60,player+8),21)==null,"owner switch suppresses pre-existing buff");
hornEffects.Reset();for(int i=0;i<3;i++)hornEffects.Observe(Song(0),i*.1);
hornEffects.Observe(Song(30),.3);hornEffects.Observe(Song(0),.4);
Check(hornEffects.Observe(Song(0),.5)==null,"one-frame effect candidate cannot claim interrupted performance succeeded");
hornEffects.Reset();for(int i=0;i<3;i++)hornEffects.Observe(Song(0),i*.1);
hornEffects.Observe(Song(30),.3);hornEffects.Observe(Song(29),.4);
Check(hornEffects.Observe(Song(28),.5)?.Kind=="horn_effect_active","three stable positive timer samples confirm activation");
Check(hornEffects.Observe(Song(100),2)==null,"read gap resets pending renewal");
// Queue contents, including damage-song IDs, cannot substitute for actual effects.
var queuedHorn=new WeaponStateCompanion();
for(int i=0;i<6;i++)
{
    var queued=new WeaponStateSnapshot("HuntingHorn",player,data,new(){["note_queue_count"]=4,["song_queue_count"]=3,["horn_song_id_slot_0_raw"]=52,["horn_song_id_slot_1_raw"]=53,["horn_play_index_raw"]=i});
    Check(queuedHorn.Observe(queued,i*.1)==null,"queued or cursor-changing damage melodies never claim an effect");
}
queuedHorn.Reset();
for(int i=0;i<3;i++)queuedHorn.Observe(Song(0),i*.1);
queuedHorn.Observe(Song(30),.3);queuedHorn.Observe(Song(29),.4);
Check(queuedHorn.Observe(Song(28),.5)?.Kind=="horn_effect_active","actual effect activates without queue IDs or a performance action");
var itemMemory=new MemoryFixture();itemMemory.Pointers[player+0x7D20]=0x400000;
itemMemory.Values[0x4006D4]=1;itemMemory.Values[0x4006D8]=2;itemMemory.Values[0x4006A4]=10;itemMemory.Float(0x4006A0,60);itemMemory.Float(0x4006C4,30);itemMemory.Float(0x4006C8,0);
var itemFields=ItemEffectProbe.Read(itemMemory,player);
Check(itemFields["item_effect_demondrug"]==1&&itemFields["item_effect_mega_demondrug"]==0,"permanent attack item effect uses native selector rather than timer");
Check(itemFields["item_effect_mega_armorskin"]==1&&itemFields["item_effect_armorskin"]==0,"normal and mega defense effects exclusive");
Check(itemFields["item_effect_might_seed"]==60&&itemFields["item_effect_might_pill"]==0,"seed requires matching native subtype");
itemMemory.Values[0x4006A4]=25;Check(ItemEffectProbe.Read(itemMemory,player)["item_effect_might_pill"]==60,"pill distinguished from shared seed timer");
itemMemory.Values[0x4006D4]=9;Check(!ItemEffectProbe.Read(itemMemory,player).ContainsKey("item_effect_demondrug"),"invalid permanent selector remains unknown");
itemMemory.Float(0x4006C4,float.NaN);Check(!ItemEffectProbe.Read(itemMemory,player).ContainsKey("item_effect_demon_powder"),"invalid item timer remains unknown");
var itemRace=new MemoryFixture{ChangePointer=true};itemRace.Pointers[player+0x7D20]=0x400000;itemRace.Float(0x4006C4,30);Check(ItemEffectProbe.Read(itemRace,player).Count==0,"abnormality component identity change discards item effects");
var itemStates=new WeaponStateCompanion();var itemSnap=new WeaponStateSnapshot("Hammer",player,data,new(){["item_effect_demondrug"]=0});
for(int i=0;i<3;i++)itemStates.Observe(itemSnap,i*.1);
itemSnap=itemSnap with{Fields=new(){["item_effect_demondrug"]=1}};
Check(itemStates.Observe(itemSnap,.3)==null&&itemStates.Observe(itemSnap,.4)==null,"item effects need stable observations");
var itemReaction=itemStates.Observe(itemSnap,.5);
Check(itemReaction?.Text.Contains("效果生效")==true&&itemReaction.Context.Contains("可能由队友广域"),"item response names effect without claiming local item use");
Check(itemStates.Observe(itemSnap,.6)==null,"held permanent effect does not repeat");
itemStates.Reset();for(int i=0;i<3;i++)Check(itemStates.Observe(itemSnap,i*.1)==null,"initial permanent effect remains silent");
var recoveries=new PlayerRecoveryEvents();
Check(!recoveries.Observe(50,150,0)&&recoveries.Observe(85,150,.1),"significant HP gain responds without item attribution");
Check(!recoveries.Observe(120,150,.2),"recovery cooldown suppresses repeated healing ticks");
recoveries.Reset();Check(!recoveries.Observe(0,150,0)&&!recoveries.Observe(150,150,.1),"return from zero HP does not fabricate potion use");
recoveries.Reset();recoveries.Observe(50,100,0);Check(!recoveries.Observe(100,150,.1),"max HP change is not a recovery event");
recoveries.Reset();recoveries.Observe(50,150,0);Check(!recoveries.Observe(100,150,2),"sampling gap cannot establish recovery");
recoveries.Reset();recoveries.Observe(50,150,0);for(int i=1;i<=20;i++)Check(!recoveries.Observe(50+i,150,i*.5),"slow small regeneration does not chatter");
recoveries.Reset();recoveries.Observe(50,150,0);recoveries.Observe(65,150,.1);recoveries.Observe(40,150,.2);Check(!recoveries.Observe(60,150,.3),"damage resets accumulated recovery");
Check(!recoveries.Observe(float.NaN,150,.4),"invalid HP cancels recovery observation");
var consumableTimers=new MemoryFixture();consumableTimers.Pointers[player+0x7D20]=0x400000;
foreach(uint offset in new uint[]{0x690,0x698,0x6EC,0x6F0,0x6F8})consumableTimers.Float(0x400000+offset,10);
Check(ItemEffectProbe.Read(consumableTimers,player).Count==5,"additional consumable effect timers are read independently");
var catNumbers=new byte[0xB8C0];
BitConverter.GetBytes(120f).CopyTo(catNumbers,0x7000);BitConverter.GetBytes(150f).CopyTo(catNumbers,0x7004);BitConverter.GetBytes(100).CopyTo(catNumbers,0x7008);BitConverter.GetBytes(float.NaN).CopyTo(catNumbers,0x700C);
var catCandidates=PalicoHealthProbe.Candidates(catNumbers);
Check(catCandidates.Any(x=>x.Offset==0x7000&&x.Float==120)&&catCandidates.Any(x=>x.Offset==0x7008&&x.Integer==100),"cat research retains float and integer numeric candidates without declaring HP");
Check(!catCandidates.Any(x=>x.Offset==0x700C),"cat research rejects nonfinite number");
Check(PalicoHealthProbe.Candidates(new byte[8]).Count==0,"cat research refuses short snapshots");
Check(CombatHookIsolation.Allowed,"user-authorized retest enables native contact and displayed-damage observer startup");
var catType=new MemoryFixture();const ulong catModule=0x10000000,catActor=0x500000,catTable=0x10001000,catGetter=0x10002000,catDti=0x10003000,catName=0x10004000;
catType.Pointers[catActor]=catTable;catType.Pointers[catTable+32]=catGetter;
var getterBytes=new byte[]{0x48,0x8D,5,0,0,0,0,0xC3};BitConverter.GetBytes((int)(catDti-catGetter-7)).CopyTo(getterBytes,3);
catType.Pointers[catGetter]=BitConverter.ToUInt64(getterBytes);catType.Pointers[catDti+8]=catName;catType.Pointers[catName]=BitConverter.ToUInt64(new byte[]{117,79,116,111,109,111,0,0});
Check(PalicoTypeProbe.IsPalico(catType,catActor,catModule),"safe cat type decoder recognizes static uOtomo without native calls");
catType.Pointers[catName]=BitConverter.ToUInt64(new byte[]{117,79,116,48,48,48,0,0});
Check(PalicoTypeProbe.IsPalico(catType,catActor,catModule),"actual game uOt000 Palico type recognized by exact bounded name");
catType.Values[catDti+0x30]=0xC1E0/4;
Check(PalicoTypeProbe.ObjectSize(catType,catActor,catModule)==0xC1E0,"current DTI size includes derived Palico tail rather than truncating base object");
catType.Values[catDti+0x30]=0x40000;Check(PalicoTypeProbe.ObjectSize(catType,catActor,catModule)==0,"implausibly large cat object size rejected");
var healthName=System.Text.Encoding.ASCII.GetBytes("cpHealthManager\0");
catType.Pointers[catName]=BitConverter.ToUInt64(healthName,0);catType.Pointers[catName+8]=BitConverter.ToUInt64(healthName,8);
Check(PalicoTypeProbe.IsHealthManager(catType,catActor,catModule),"health component requires exact compiled class identity");
Check(!PalicoTypeProbe.IsPalico(catType,catActor,catModule),"health component cannot masquerade as cat entity");
catType.Pointers[catName]=BitConverter.ToUInt64(new byte[]{117,79,116,48,48,48,80,0});
Check(!PalicoTypeProbe.IsPalico(catType,catActor,catModule),"uOt000 prefix does not accept preview or accessory subclass");
catType.Pointers[catName]=BitConverter.ToUInt64(new byte[]{117,79,116,111,109,111,80,0});Check(!PalicoTypeProbe.IsPalico(catType,catActor,catModule),"safe cat type decoder excludes preview/subtype names");
catType.Pointers[catGetter]=0x9090909090909090;Check(!PalicoTypeProbe.IsPalico(catType,catActor,catModule),"unrecognized native getter shape stays silent instead of calling it");
catType.Pointers[catTable+32]=0x90000000;Check(!PalicoTypeProbe.IsPalico(catType,catActor,catModule),"getter outside game image range rejected");
Check(!PalicoTypeProbe.IsPalico(new MemoryFixture(),catActor,0),"missing module or object cannot identify cat");
Check(PalicoTypeProbe.Describe(new MemoryFixture(),catActor,catModule)=="stage=object_unreadable","cat rejection diagnostics distinguish unreadable object without native calls");
Check(PalicoTypeProbe.Describe(catType,catActor,catModule).Contains("getter_outside_image"),"cat rejection diagnostics distinguish getter range failure");
var catRecovery=new PalicoRecoveryDirector();var catHealth=new PalicoHealthSample(1,2,400,550,0);
Check(!catRecovery.Observe(catHealth,true),"initial wounded cat does not fabricate recovery");
Check(catRecovery.Observe(catHealth with{Current=540,Time=6},true),"observed quarter-max recovery from below seventy-five percent triggers cat response");
Check(!catRecovery.Observe(catHealth with{Current=540,Time=8},true),"cat recovery cooldown suppresses repeated gain");
catRecovery.Reset();catRecovery.Observe(catHealth,true);
Check(!catRecovery.Observe(catHealth with{Component=3,Current=550,Time=1},true),"component replacement does not fabricate recovery");
catRecovery.Reset();catRecovery.Observe(catHealth,true);
Check(!catRecovery.Observe(catHealth with{Maximum=600,Current=550,Time=1},true),"cat max HP change remains silent");
catRecovery.Reset();catRecovery.Observe(catHealth with{Current=0},true);
Check(!catRecovery.Observe(catHealth with{Current=550,Time=1},true),"cat return from zero HP is not healing feedback");
catRecovery.Reset();catRecovery.Observe(catHealth,true);
Check(!catRecovery.Observe(catHealth with{Current=550,Time=16},true),"long cat sample gap cannot establish recovery");
catRecovery.Reset();catRecovery.Observe(catHealth,false);
Check(!catRecovery.Observe(catHealth with{Current=550,Time=1},false),"disabled cat feedback stays silent");
Check(!PalicoHealthReader.TryRead(new MemoryFixture(),catActor,catModule,0,"unknown",out _),"cat direct HP route rejects unknown revision");
var replayPath=Environment.GetEnvironmentVariable("MHW_PALICO_REPLAY_LOG");
if(!string.IsNullOrEmpty(replayPath))
{
 var replayDirector=new PalicoRecoveryDirector();int count=0,responses=0;
 foreach(var line in File.ReadLines(replayPath))
 {
  const string marker="PALICO_HEALTH_CANDIDATE ";int at=line.IndexOf(marker,StringComparison.Ordinal);if(at<0)continue;
  using var doc=System.Text.Json.JsonDocument.Parse(line[(at+marker.Length)..]);var root=doc.RootElement;
  if(!root.TryGetProperty("health_components",out var components))continue;
  foreach(var c in components.EnumerateArray())if(c.GetProperty("pointer_offset").GetInt32()==0x7F58)
  {
   count++;if(replayDirector.Observe(new(1,2,c.GetProperty("current").GetSingle(),c.GetProperty("maximum").GetSingle(),root.GetProperty("time").GetDouble()),true))responses++;
  }
 }
 Check(count>0&&responses>0,"actual cat numeric trace replay produces recovery events; synthetic identity only");
 Console.WriteLine($"Palico numeric replay: {count} samples, {responses} recovery events (identity and game output not verified by replay)");
}
var pendingRecovery=new PendingRecoveryFeedback();pendingRecovery.Offer(0);
Check(!pendingRecovery.TryTake(1,false)&&pendingRecovery.TryTake(8,true),"cat recovery waits through simultaneous player healing quiet period");
Check(!pendingRecovery.TryTake(9,true),"pending recovery emits once");
pendingRecovery.Offer(20);Check(!pendingRecovery.TryTake(33,true),"stale recovery does not speak late");
pendingRecovery.Offer(40);pendingRecovery.Reset();Check(!pendingRecovery.TryTake(41,true),"scene reset clears pending recovery");
catRecovery.Reset();catRecovery.Observe(catHealth,true);Check(catRecovery.Observe(catHealth with{Current=540,Time=1},true),"unattributed cat recovery only needs validated state change, no party-source attribution");
catRecovery.Reset();catRecovery.Observe(catHealth with{Current=412.5f},true);
Check(!catRecovery.Observe(catHealth with{Current=550,Time=1},true),"exactly seventy-five percent HP does not arm cat recovery");
catRecovery.Reset();catRecovery.Observe(catHealth with{Current=500},true);
Check(!catRecovery.Observe(catHealth with{Current=550,Time=1},true),"high-health top-up stays silent");
catRecovery.Reset();catRecovery.Observe(catHealth with{Current=300},true);
Check(!catRecovery.Observe(catHealth with{Current=437.49f,Time=1},true)&&catRecovery.Observe(catHealth with{Current=437.5f,Time=2},true),"quarter-max threshold is inclusive at 137.5 of 550 HP");
catRecovery.Reset();catRecovery.Observe(catHealth with{Current=350},true);
Check(!catRecovery.Observe(catHealth with{Current=400,Time=1},true)&&!catRecovery.Observe(catHealth with{Current=450,Time=2},true)&&catRecovery.Observe(catHealth with{Current=490,Time=3},true),"multi-tick recovery keeps armed baseline after crossing seventy-five percent");
catRecovery.Reset();catRecovery.Observe(catHealth with{Current=300},true);catRecovery.Observe(catHealth with{Current=400,Time=1},true);catRecovery.Observe(catHealth with{Current=350,Time=2},true);
Check(!catRecovery.Observe(catHealth with{Current=450,Time=3},true),"damage resets recovery baseline and prevents summing repeated heal damage cycles");
catRecovery.Reset();catRecovery.Observe(catHealth with{Current=300},true);catRecovery.Observe(catHealth with{Current=350,Time=1},true);
for(int i=2;i<=12;i++)Check(!catRecovery.Observe(catHealth with{Current=350,Time=i},true),"stalled recovery does not produce event");
Check(!catRecovery.Observe(catHealth with{Current=440,Time=13},true),"expired recovery period discards old partial healing");
Check(MonsterBreakProbe.DefinitionCount==252,"World break threshold metadata loaded independently of player weapon rules");
var breakMemory=new MemoryFixture();const ulong breakMonster=0x100000,breakTable=0x500000;breakMemory.Pointers[breakMonster+0x1D058]=breakTable;breakMemory.Values[breakMonster+0x12280]=0;
const ulong breakAt=breakTable+0x40;breakMemory.Values[breakAt+0x6C]=0;breakMemory.Values[breakAt+0x18]=4;breakMemory.Float(breakAt+0xC,1000);breakMemory.Float(breakAt+0x10,800);
var breakRecords=MonsterBreakProbe.Read(breakMemory,breakMonster,"421810",0);
Check(breakRecords is [{Part:2,Counter:4}]&&breakRecords[0].Thresholds.Contains(5),"normal part uses schema slot when live Index is zero, validates HP and threshold");
Check(MonsterBreakProbe.Read(breakMemory,breakMonster,"unknown",0).Length==0,"monster break probe rejects unknown revision");
var breakTracker=new MonsterBreakTracker();
MonsterBreakSample BreakAt(int count,double t)=>new(breakMonster,breakTable,0,2,count,[5,8],t);
Check(breakTracker.Observe([BreakAt(0,0)],0).Length==0,"already observed initial counter is not a break");
Check(breakTracker.Observe([BreakAt(1,.25)],.25).Length==0&&breakTracker.Observe([BreakAt(1,.5)],.5).Length==0,"ordinary flinch counter below schema threshold is silent");
Check(breakTracker.Observe([BreakAt(5,.75)],.75).Length==0,"threshold crossing waits for second sample");
Check(breakTracker.Observe([BreakAt(5,1)],1) is [{Threshold:5,Previous:1,Current:5}],"stable threshold crossing yields one break candidate");
Check(breakTracker.Observe([BreakAt(5,1.25)],1.25).Length==0,"stable broken counter does not repeat");
Check(breakTracker.Observe([BreakAt(8,1.5)],1.5).Length==0&&breakTracker.Observe([BreakAt(8,1.75)],1.75) is [{Threshold:8}],"second break threshold remains distinct from first");
breakTracker.Reset();breakTracker.Observe([BreakAt(0,0)],0);breakTracker.Observe([BreakAt(5,.25)],.25);
Check(breakTracker.Observe([BreakAt(0,.5)],.5).Length==0,"counter spike followed by reset cannot confirm break");
breakTracker.Reset();breakTracker.Observe([BreakAt(5,0)],0);
Check(breakTracker.Observe([BreakAt(5,.25)],.25).Length==0,"loading already broken monster stays silent");
breakTracker.Reset();breakTracker.Observe([BreakAt(0,0)],0);breakTracker.Observe([BreakAt(5,.25)],.25);
Check(breakTracker.Observe([BreakAt(5,2)],2).Length==0,"sampling gap cannot confirm old break candidate");
var encounterMemory=new MemoryFixture();const ulong encounterMonster=0x100000,ailmentBase=0x700000;
encounterMemory.Values[encounterMonster+0x12280]=0;encounterMemory.Values[encounterMonster+0x6274]=2;encounterMemory.Values[encounterMonster+0x6278]=10;
encounterMemory.Pointers[encounterMonster+0x1BC40]=ailmentBase;encounterMemory.Pointers[encounterMonster+0x1BC48]=0;
const ulong ailmentAt=ailmentBase+0x148;encounterMemory.Values[ailmentAt+0x10]=7;encounterMemory.Values[ailmentAt+8]=1;encounterMemory.Float(ailmentAt+0x14,10);encounterMemory.Float(ailmentAt+0x70,9);encounterMemory.Values[ailmentAt+0x78]=1;
var stunSample=MonsterEncounterProbe.Read(encounterMemory,encounterMonster,0,"421810",0);
Check(stunSample?.Ailments is [{Id:7,Active:true,Duration:9,Counter:1}],"native World stun status uses typed active timer and counter fields");
Check(stunSample?.ActionType=="unknown","unreadable monster action type stays unknown without native calls");
Check(MonsterEncounterProbe.Read(encounterMemory,encounterMonster,0,"unknown",0)==null,"encounter probe rejects unknown version");
encounterMemory.Values[ailmentAt+0x10]=14;Check(MonsterEncounterProbe.Read(encounterMemory,encounterMonster,0,"421810",.1)?.Ailments is [{Id:14}],"shock trap has independent native status ID");
encounterMemory.Values[ailmentAt+0x10]=15;Check(MonsterEncounterProbe.Read(encounterMemory,encounterMonster,0,"421810",.2)?.Ailments is [{Id:15}],"pitfall trap has independent native status ID");
encounterMemory.Float(ailmentAt+0x70,float.NaN);Check(MonsterEncounterProbe.Read(encounterMemory,encounterMonster,0,"421810",.3)?.Ailments.Length==0,"invalid monster status timer is not a successful event");
var encounterTrace=new MonsterEncounterTrace();
Check(encounterTrace.Observe(stunSample!)!=null,"initial encounter baseline is labelled and available for research");
Check(encounterTrace.Observe(stunSample! with{Time=.25,Ailments=[new(7,true,8.75f,10,1)]})==null,"ordinary active stun countdown does not spam state transitions");
Check(encounterTrace.Observe(stunSample! with{Time=.5,Ailments=[new(7,false,0,10,1)]})!=null,"stun end is captured independently from start");
Check(encounterTrace.Observe(stunSample! with{Time=.75,ActionId=11,Ailments=[new(7,false,0,10,1)]})!=null,"monster action transition captured without assuming wallbang");
var stateResponses=new MonsterStateDirector();
MonsterEncounterSample State(double t,bool stun=false,bool paralysis=false,bool pitfall=false,bool shock=false,string action="nActEm057::Idle",bool sleep=false)=>new(1,94,1,0,action,1000,1000,[new(3,sleep,0,20,0),new(7,stun,0,7,0),new(2,paralysis,0,5,0),new(15,pitfall,0,10,0),new(14,shock,0,10,0)],t);
stateResponses.Observe(State(0));stateResponses.Observe(State(.25,paralysis:true));stateResponses.Observe(State(.5,paralysis:true));
Check(stateResponses.Take(.5,true)?.Kind=="paralysis","paralysis produces paralysis response, never stun");
stateResponses.Observe(State(.75));stateResponses.Observe(State(1,action:"nActEm057::DamageDown"));Check(stateResponses.Take(1,true)==null,"ordinary or mounting knockdown is not a stun");
stateResponses.Observe(State(1.25,stun:true));stateResponses.Observe(State(1.5,stun:true));Check(stateResponses.Take(1.5,true)?.Kind=="stun","stable explicit native stun produces one response");
stateResponses.Observe(State(1.75,stun:true));Check(stateResponses.Take(1.75,true)==null,"stun duration does not repeat");
stateResponses.Reset();stateResponses.Observe(State(0));stateResponses.Observe(State(.25));Check(stateResponses.Take(.25,true)==null,"trap placement without native trap active stays silent");
stateResponses.Observe(State(.5,pitfall:true));stateResponses.Observe(State(.75,pitfall:true));Check(stateResponses.Take(.75,true)?.Kind=="pitfall_trap","pitfall actual capture requires stable active state");
stateResponses.Reset();stateResponses.Observe(State(0,action:"nActEm057::DamageClawGrabCarry"));stateResponses.Observe(State(.25,action:"nActEm057::DamageClawGrabHitWall"));Check(stateResponses.Take(.25,true)?.Kind=="wallbang","explicit carry to wall collision produces wallbang response");
stateResponses.Reset();stateResponses.Observe(State(0,stun:true));stateResponses.Observe(State(.25,stun:true));Check(stateResponses.Take(.25,true)==null,"initial already stunned state does not replay success");
var monsterReplay=Path.GetFullPath("research/monster-state/zinogre-20261027-state-replay.json");
if(File.Exists(monsterReplay))
{
 var frames=System.Text.Json.JsonSerializer.Deserialize<MonsterEncounterSample[]>(File.ReadAllText(monsterReplay))!;
 var replayStates=new MonsterStateDirector();var totals=new Dictionary<string,int>();MonsterEncounterSample? prev=null;
 void Tick(MonsterEncounterSample frame){replayStates.Observe(frame);while(replayStates.Take(frame.Time,true) is {} result)totals[result.Kind]=totals.GetValueOrDefault(result.Kind)+1;}
 foreach(var frame in frames){if(prev!=null)for(double t=prev.Time+.25;t<frame.Time;t+=.25)Tick(prev with{Time=t});Tick(frame);prev=frame;}
 Check(totals.GetValueOrDefault("paralysis")==1&&totals.GetValueOrDefault("stun")==2&&totals.GetValueOrDefault("pitfall_trap")==1&&totals.GetValueOrDefault("shock_trap")==0&&totals.GetValueOrDefault("wallbang")==5,"normalized actual trace replay respects corrected user markers and inactive shock trap");
 Console.WriteLine("Monster replay (held states between logged transitions): "+System.Text.Json.JsonSerializer.Serialize(totals));
}
stateResponses.Reset();stateResponses.Observe(State(0));stateResponses.Observe(State(.25,sleep:true));stateResponses.Observe(State(.5,sleep:true));Check(stateResponses.Take(.5,true)?.Kind=="sleep","sleep native ID stays distinct from paralysis and stun");
stateResponses.Reset();stateResponses.Observe(State(0,shock:false));stateResponses.Observe(State(.25,shock:true));stateResponses.Observe(State(.5,shock:true));Check(stateResponses.Take(.5,true)?.Kind=="shock_trap","shock trap active is distinct from paralysis weapon status");
stateResponses.Reset();stateResponses.Observe(State(0) with{Ailments=[new(5,false,0,0,0)]});stateResponses.Observe(State(.25,action:"nActEm057::DamageRideAccept") with{Ailments=[new(5,false,0,0,1)]});Check(stateResponses.Take(.25,true)?.Kind=="mount_started","mount counter plus explicit accept action identifies mounting start");
stateResponses.Observe(State(.5,action:"nActEm057::DamageDown") with{Ailments=[new(5,false,0,0,1)]});Check(stateResponses.Take(.5,true)==null,"mount knockdown is never renamed stun or assumed success");
var layoutReplay=Path.GetFullPath("research/monster-state/zinogre-multiplayer-layout-20261027.json");
if(File.Exists(layoutReplay))
{
 using var layouts=System.Text.Json.JsonDocument.Parse(File.ReadAllText(layoutReplay));var bm=new MemoryFixture();const ulong rootMonster=0x100000,partRoot=0x500000;
 bm.Pointers[rootMonster+0x1D058]=partRoot;bm.Values[rootMonster+0x12280]=94;var bt=new MonsterBreakTracker();var tt=new TenderizeTracker();int breaks=0,softens=0;double clockTime=0;
 foreach(var layout in layouts.RootElement.EnumerateArray())
 {
  foreach(var row in layout.GetProperty("part_rows").EnumerateArray())
  {ulong at=partRoot+0x40+(uint)row.GetProperty("slot").GetInt32()*0x1F8;bm.Values[at+0x6C]=row.GetProperty("index").GetInt32();bm.Values[at+0x18]=row.GetProperty("counter").GetInt32();bm.Float(at+0xC,row.GetProperty("maximum").GetSingle());bm.Float(at+0x10,row.GetProperty("current").GetSingle());}
  foreach(var row in layout.GetProperty("tenderize_rows").EnumerateArray())
  {ulong at=rootMonster+0x1C458+(uint)row.GetProperty("slot").GetInt32()*64;bm.Values[at+48]=row.GetProperty("part").GetInt32();bm.Float(at+8,row.GetProperty("current").GetSingle());bm.Float(at+12,row.GetProperty("maximum").GetSingle());}
  // Sparse diagnostic layouts replayed as stable two-sample plateaus; no claim of original timing.
  for(int repeat=0;repeat<2;repeat++){clockTime+=.25;breaks+=bt.Observe(MonsterBreakProbe.Read(bm,rootMonster,"421810",clockTime),clockTime).Length;softens+=tt.Observe(TenderizeProbe.Read(bm,rootMonster,"421810",clockTime),clockTime).Length;}
 }
 Check(breaks==3&&softens==1,"actual multiplayer layouts replay as three schema breaks and one active wound; elapsed expiry does not retrigger");
 Console.WriteLine($"Multiplayer layout replay (stable plateaus): breaks={breaks}, softens={softens}");
}
stateResponses.Reset();stateResponses.Observe(State(0) with{Ailments=[new(5,false,0,0,0),new(11,false,0,0,0)]});
stateResponses.Observe(State(.25,action:"nActEm042Common::DamageRideAccept") with{Ailments=[new(5,false,0,0,1),new(11,false,0,0,0)]});stateResponses.Take(.25,true);
stateResponses.Observe(State(.5,action:"nActEm042Common::RideTired") with{Ailments=[new(5,false,0,0,1),new(11,false,0,0,0)]});
stateResponses.Observe(State(.75,action:"nActEm042Common::DamageRideDown") with{Ailments=[new(5,false,0,0,1),new(11,false,0,0,1)]});Check(stateResponses.Take(.75,true)?.Kind=="mount_knockdown","explicit ride-down branch and increased knockdown counter after mounting confirm distinct result");
stateResponses.Observe(State(1,action:"nActEm042Common::DamageRideDown") with{Ailments=[new(5,false,0,0,1),new(11,false,0,0,1)]});Check(stateResponses.Take(1,true)==null,"held mounted knockdown does not repeat");
stateResponses.Reset();stateResponses.Observe(State(0) with{Ailments=[new(11,false,0,0,0)]});stateResponses.Observe(State(.25,action:"nActEm042Common::DamageRideDown") with{Ailments=[new(11,false,0,0,1)]});Check(stateResponses.Take(.25,true)==null,"isolated ride-down without observed mounting history stays silent");
var sleepReplay=Path.GetFullPath("research/monster-state/sleep-trap-mount-20261027-state-replay.json");
if(File.Exists(sleepReplay))
{
 var frames=System.Text.Json.JsonSerializer.Deserialize<MonsterEncounterSample[]>(File.ReadAllText(sleepReplay))!;var replayStates=new MonsterStateDirector();var totals=new Dictionary<string,int>();MonsterEncounterSample? prev=null;
 void TickSleep(MonsterEncounterSample frame){replayStates.Observe(frame);while(replayStates.Take(frame.Time,true) is {} result)totals[result.Kind]=totals.GetValueOrDefault(result.Kind)+1;}
 foreach(var frame in frames){if(prev!=null)for(double t=prev.Time+.25;t<frame.Time;t+=.25)TickSleep(prev with{Time=t});TickSleep(frame);prev=frame;}
 Check(totals.GetValueOrDefault("sleep")==1&&totals.GetValueOrDefault("shock_trap")==1&&totals.GetValueOrDefault("paralysis")==1&&totals.GetValueOrDefault("mount_knockdown")==1,"actual corrected sleep/trap/mount trace yields distinct native state responses including explicit ride-down");
 Console.WriteLine("Sleep/trap/mount replay (held states): "+System.Text.Json.JsonSerializer.Serialize(totals));
}
var stateQueue=new WeaponStateResponseQueue();var liveBuff=new WeaponStateSnapshot("Hammer",player,data,new(){["item_effect_demondrug"]=1});
var buffReaction=new ActionReaction("weapon_state_item_effect_demondrug","效果生效","happy",.5f,"state only"){RequiredStateFields=["item_effect_demondrug"]};
stateQueue.Offer(buffReaction,liveBuff,0);Check(stateQueue.Take(liveBuff,1,false)==null&&stateQueue.Take(liveBuff,8,true)==buffReaction,"confirmed item effect waits for health chatter without being lost");
Check(stateQueue.Take(liveBuff,9,true)==null,"queued buff emits once");
stateQueue.Offer(buffReaction,liveBuff,10);Check(stateQueue.Take(liveBuff with{Fields=new(){["item_effect_demondrug"]=0}},11,true)==null,"expired native buff is not announced after wait");
stateQueue.Offer(buffReaction,liveBuff,20);Check(stateQueue.Take(liveBuff with{Data=data+8},21,true)==null,"changed weapon instance invalidates queued state fact");
stateQueue.Offer(buffReaction,liveBuff,30);Check(stateQueue.Take(liveBuff,43,true)==null,"stale queued state fact is discarded");
stateQueue.Offer(buffReaction,liveBuff,50);Check(stateQueue.Take(null,51,true)==null&&stateQueue.Take(liveBuff,52,true)==null,"missing state sample clears pending facts");
Check(refreshed?.RequiredStateFields.Contains("song_effect_3C_raw")==true,"horn refresh retains exact observed timer field for delivery validation");
var hornQueue=new WeaponStateResponseQueue();hornQueue.Offer(refreshed!,Song(50),60);Check(hornQueue.Take(Song(0),61,true)==null,"horn buff that ended before delivery is not announced");
var songQueueMemory=new MemoryFixture();const ulong hornRoot=0x300000;
songQueueMemory.Pointers[hornRoot+0x2470]=3;songQueueMemory.Pointers[hornRoot+0x24A8]=0;songQueueMemory.Pointers[hornRoot+0x24B0]=0;songQueueMemory.Values[hornRoot+0x24B8]=0;
songQueueMemory.Values[hornRoot+0x2498]=52;songQueueMemory.Values[hornRoot+0x249C]=53;songQueueMemory.Values[hornRoot+0x24A0]=3;
var songRaw=HornSongQueueProbe.Read(songQueueMemory,hornRoot);
Check(songRaw["horn_song_id_slot_0_raw"]==52&&songRaw["horn_song_id_slot_1_raw"]==53,"read native song ID queue as raw facts without declaring release");
songQueueMemory.Values[hornRoot+0x24B8]=3;Check(HornSongQueueProbe.Read(songQueueMemory,hornRoot).Count==0,"invalid song start index rejects optional queue fields");
Check(HornSongQueueProbe.Read(new MemoryFixture(),hornRoot).Count==0,"missing song queue remains unknown instead of a zero melody");
var gsDirector=new CombatDamageDirector();
var gsFacts=new CombatFacts(player,"GreatSword",null,new(),0);
var gsSettings=new SaedResponseSettings{DynamicThreshold=false,ReferenceDamage=100};
var gsStart=new NativeActionSample(player,"GreatSword",3,78,0);
Check(gsDirector.Enter(new(gsStart with{Id=0},gsStart),new(new("GreatSword",3,78,0,player,true),gsFacts,baseCounter),baseCounter,gsSettings),"TCS start confirmed");
gsDirector.Observe(baseCounter with{Time=.2,Total=baseCounter.Total+30},gsFacts with{Time=.2},.2);
var gsPowered=gsStart with{Id=135,Time=.3};
gsDirector.Enter(new(gsStart,gsPowered),null,baseCounter with{Time=.3,Total=baseCounter.Total+30},gsSettings);
gsDirector.Observe(baseCounter with{Time=.4,Total=baseCounter.Total+100},gsFacts with{Time=.4},.4);
gsDirector.Enter(new(gsPowered,gsStart with{Id=0,Time=.5}),null,baseCounter with{Time=.5,Total=baseCounter.Total+100},gsSettings);
var gsVerdict=gsDirector.Observe(baseCounter with{Time=.5,Total=baseCounter.Total+1000},gsFacts with{Time=.5},.5);
Check(gsVerdict is {Label:"真蓄力斩（强击）",Damage:70},"powered TCS updates label and excludes setup and following damage");
var danceDirector=new CombatDamageDirector();
var danceFacts=new CombatFacts(player,"DualBlades",null,new Dictionary<string,double>{{"demon_mode",1}},0);
var danceSettings=new SaedResponseSettings{DynamicThreshold=false,ReferenceDamage=100,WindowSeconds=5};
var danceStart=new NativeActionSample(player,"DualBlades",3,92,0);
Check(danceDirector.Enter(new(danceStart with{Id=0},danceStart),new(new("DualBlades",3,92,0,player,true),danceFacts,baseCounter),baseCounter,danceSettings),"dance starts only on confirmed action");
for(int i=1;i<=60;i++)Check(danceDirector.Observe(baseCounter with{Time=i*.1,Total=baseCounter.Total+i},danceFacts with{Time=i*.1},i*.1)==null&&danceDirector.Active,"director holds dance until action exit rather than five seconds");
var danceExit=new NativeActionSample(player,"DualBlades",3,65,6.1);
danceDirector.Enter(new(danceStart with{Time=6},danceExit),null,baseCounter with{Time=6.1,Total=baseCounter.Total+80},danceSettings);
Check(danceDirector.Observe(baseCounter with{Time=6.1,Total=baseCounter.Total+500},danceFacts with{Time=6.1},6.1)?.Damage==80,"dance exit seals damage before follow-up hits");
Check(danceDirector.Observe(baseCounter with{Time=6.2,Total=baseCounter.Total+600},danceFacts with{Time=6.2},6.2)==null,"dance director emits one verdict per action");
// Completion-driven windows must validate every sample without timing out into repeated praise.
window.Reset();window.Start(baseCounter,0,settings);
for(int i=1;i<=60;i++)Check(window.Observe(baseCounter with{Time=i*.1,Total=baseCounter.Total+i},i*.1,waitForCompletion:true)==null&&window.Active,"completion window remains pending through damage ticks");
Check(window.Observe(baseCounter with{Time=6.1,Total=baseCounter.Total+180},6.1,complete:true,waitForCompletion:true)?.Damage==180,"exit seals one total damage verdict");
Check(window.Observe(baseCounter with{Time=6.2,Total=baseCounter.Total+300},6.2,complete:true,waitForCompletion:true)==null,"later combo cannot re-evaluate completed dance window");
window.Start(baseCounter,0,settings);
Check(window.Observe(baseCounter with{Time=1},1,waitForCompletion:true)==null&&!window.Active,"completion wait still cancels missing counter samples");
var wrongEvasionBase=new MemoryFixture(); wrongEvasionBase.Pointers[0x107D20]=0x200000; wrongEvasionBase.Float(0x200FC4,12); Check(!NativeEvasionSignal.TryRead(wrongEvasionBase,0x100000,"421810",out _),"ordinary abnormality base cannot stand in for gear buff base");
evasionMemory.Float(0x200FC4,BitConverter.Int32BitsToSingle(65)); Check(!NativeEvasionSignal.TryRead(evasionMemory,0x100000,"421810",out _),"observed integer-like subnormal is not a valid mantle timer");
evasionMemory.Float(0x200FC4,float.NaN); Check(!NativeEvasionSignal.TryRead(evasionMemory,0x100000,"421810",out _),"mantle timer rejects invalid float");
evasionMemory.Float(0x200FC4,12); evasionMemory.ChangePointer=true; Check(!NativeEvasionSignal.TryRead(evasionMemory,0x100000,"421810",out _),"mantle timer rejects changed component identity");
var evadeDirector=new NativeEvasionCompanion();
Check(evadeDirector.Observe(1,"LongSword",20,0)==null,"already active mantle on initial sample is silent");
evadeDirector.Observe(1,"LongSword",0,.1);evadeDirector.Observe(1,"LongSword",20,.2);evadeDirector.Observe(1,"LongSword",19.9f,.3);
Check(evadeDirector.Observe(1,"LongSword",19.8f,.4)?.Kind=="evasion_mantle_active","stable native buff activation responds once");
Check(evadeDirector.Observe(1,"LongSword",18.7f,.5)==null,"normal mantle countdown does not repeat");
evadeDirector.Observe(1,"LongSword",20,.6);evadeDirector.Observe(1,"LongSword",19.9f,.7);
Check(evadeDirector.Observe(1,"LongSword",19.8f,.8)==null,"mantle refresh cooldown suppresses chatter");
evadeDirector.Reset();evadeDirector.Observe(1,"Bow",10,0);evadeDirector.Observe(1,"Bow",20,.1);evadeDirector.Observe(1,"Bow",19.9f,.2);
Check(evadeDirector.Observe(1,"Bow",19.8f,.3)?.Kind=="evasion_mantle_refresh","same native buff result supports bow without inferring slide success");
Check(evadeDirector.Observe(2,"Bow",20,.4)==null,"owner replacement is initially silent");
Check(evadeDirector.Observe(2,"Bow",null,.5)==null,"missing mantle read clears pending result");
Check(evadeDirector.Observe(2,"Bow",20,2)==null,"sample gap or reconnect does not invent activation");
Check(EvasionActionScope.Identify("LongSword",1,53)=="common_roll","observed common roll remains in scope");
Check(EvasionActionScope.Identify("Bow",3,85)=="bow_charge_step","bow charging sidestep is in scope");
Check(EvasionActionScope.Identify("DualBlades",3,94)=="db_demon_rush","requested demon rush is separate from step");
Check(EvasionActionScope.Identify("DualBlades",3,105)=="db_demon_step","demon step is in scope");
Check(EvasionActionScope.Identify("LongSword",3,86)==null&&EvasionActionScope.Identify("SwordAndShield",3,125)==null,"foresight and backhop are outside new research scope");
Check(EvasionActionScope.Identify("Bow",3,94)==null&&EvasionActionScope.Identify("DualBlades",3,117)==null,"air evades are not silently added to scope");
var flagMemory=new MemoryFixture();flagMemory.Pointers[0x112608]=0x200000;flagMemory.Values[0x205998]=0;flagMemory.Values[0x2059B0]=0x02000000;
Check(EvasionFlagProbe.TryRead(flagMemory,0x100000,"421810",out var fa,out var fb)&&fa==0&&fb==0x02000000,"read upstream flag fields from gear component");
Check(!EvasionFlagProbe.TryRead(flagMemory,0x100000,"unknown",out _,out _),"unknown revision cannot read upstream flags");
var sourceMemory=new MemoryFixture();sourceMemory.Pointers[0x300008]=0x400000;sourceMemory.Values[0x40000C]=2;sourceMemory.Values[0x400060]=2;
Check(EvasionEventSource.TryResolve(sourceMemory,0x300000,out var src)&&src==0x400000,"event source fallback resolves live backing object");
Check(EvasionEventSource.Eligible(sourceMemory,src,out var sourceFlags)==true&&sourceFlags==2,"captured source eligibility bit is readable without calling native getter");
sourceMemory.Values[0x400060]=0x12;sourceMemory.Values[0x4008D8]=0x44;
Check(EvasionEventSource.Eligible(sourceMemory,src,out _)==false,"special source subtype takes priority over general eligible bit");
sourceMemory.Values[0x4008D8]=0x45;Check(EvasionEventSource.Eligible(sourceMemory,src,out _)==true,"special source requires subtype 45");
sourceMemory.Pointers[0x300038]=0x500000;sourceMemory.Pointers[0x500078]=0x600000;sourceMemory.Pointers[0x6001D8]=0x700000;sourceMemory.Values[0x70000C]=2;
Check(EvasionEventSource.TryResolve(sourceMemory,0x300000,out src)&&src==0x700000,"preferred live source precedes backing fallback");
sourceMemory.Values[0x70000C]=0;sourceMemory.Values[0x400060]=0x400;sourceMemory.Pointers[0x400B40]=0x800000;sourceMemory.Values[0x80000C]=2;
Check(EvasionEventSource.TryResolve(sourceMemory,0x300000,out src)&&src==0x800000,"source owner branch follows bit 400");
sourceMemory.Values[0x400060]=0x02000000;sourceMemory.Pointers[0x4009E0]=0x900000;sourceMemory.Values[0x90000C]=2;
Check(EvasionEventSource.TryResolve(sourceMemory,0x300000,out src)&&src==0x900000,"alternate source owner branch follows bit 2000000");
sourceMemory.Values[0x40000C]=0;Check(!EvasionEventSource.TryResolve(sourceMemory,0x300000,out _),"invalid backing source remains unknown");
var actionCodeMemory=new MemoryFixture();actionCodeMemory.Pointers[0x1062C8]=0x100000;actionCodeMemory.Pointers[0x106240]=0x200000;actionCodeMemory.Values[0x106248]=100;actionCodeMemory.Pointers[0x2001A8]=0x300000;actionCodeMemory.Pointers[0x300000]=0x400000;actionCodeMemory.Pointers[0x300028]=0x500000;
Check(ActionCodeProbe.TryLocate(actionCodeMemory,0x100000,1,53,out var codeAction,out var codeVtable,out var codeParent,out var codeStatus)&&codeAction==0x300000&&codeVtable==0x400000&&!codeParent&&codeStatus=="ok","controller-owned code lookup tolerates diagnostic Parent difference");
actionCodeMemory.Pointers[0x300030]=0x100000;Check(ActionCodeProbe.TryLocate(actionCodeMemory,0x100000,1,53,out _,out _,out codeParent,out _)&&!codeParent,"action parameter at 30 is not treated as owner");
actionCodeMemory.Pointers[0x300028]=0x100000;Check(ActionCodeProbe.TryLocate(actionCodeMemory,0x100000,1,53,out _,out _,out codeParent,out _)&&codeParent,"matching action Parent is recorded");
actionCodeMemory.Values[0x106248]=0;Check(!ActionCodeProbe.TryLocate(actionCodeMemory,0x100000,1,53,out _,out _,out _,out codeStatus)&&codeStatus=="list_count_or_pointer_invalid","empty action list reports its stage");
actionCodeMemory.Values[0x106248]=100;actionCodeMemory.Pointers[0x1062C8]=0x900000;Check(!ActionCodeProbe.TryLocate(actionCodeMemory,0x100000,1,53,out _,out _,out _,out codeStatus)&&codeStatus=="controller_owner_mismatch","foreign controller is rejected before code lookup");
Check(!ActionCodeProbe.TryLocate(actionCodeMemory,0x100000,4,53,out _,out _,out _,out codeStatus)&&codeStatus=="invalid_target","out-of-range action set is rejected");
var contactMemory=new MemoryFixture();contactMemory.Pointers[0x1006A8]=0x200000;contactMemory.Pointers[0x300080]=0x200000;contactMemory.Pointers[0x400080]=0x500000;contactMemory.Values[0x200198]=2;contactMemory.Pointers[0x2001A8]=0x100000;contactMemory.Float(0x10E168,.2f);contactMemory.Float(0x10E160,0);
var contactState=new NativeActionSample(0x100000,"HuntingHorn",1,53,10);
Check(EvasionContactProbe.TryRead(contactMemory,contactState,10.1,0,0x400000,0x300000,out var contactSample)&&contactSample!=null&&contactSample.WindowTimer==.2f&&contactSample.CollisionFlags==2&&contactSample.BodyOwnerMatches,"contact candidate requires exact current local body and records window only");
Check(!EvasionContactProbe.TryRead(contactMemory,contactState,11,0,0x400000,0x300000,out _),"stale contact context rejected");
Check(!EvasionContactProbe.TryRead(contactMemory,contactState,9,0,0x400000,0x300000,out _),"backwards contact time rejected");
contactMemory.Pointers[0x300080]=0x600000;Check(!EvasionContactProbe.TryRead(contactMemory,contactState,10.1,0,0x400000,0x300000,out _),"foreign body cannot become local contact");contactMemory.Pointers[0x300080]=0x200000;
contactMemory.Pointers[0x400080]=0x200000;Check(!EvasionContactProbe.TryRead(contactMemory,contactState,10.1,0,0x400000,0x300000,out _),"self body pair rejected");contactMemory.Pointers[0x400080]=0x500000;
contactMemory.Float(0x10E168,float.NaN);Check(EvasionContactProbe.TryRead(contactMemory,contactState,10.1,3,0x400000,0x300000,out contactSample)&&contactSample!=null&&contactSample.WindowTimer==null&&contactSample.Result==3,"nonfinite window remains unknown and raw callback result is preserved without verdict");
Check(EvasionContactProbe.TryRead(contactMemory,contactState with {Id=0},10.1,0,0x400000,0x300000,out contactSample)&&contactSample?.Scope==null,"standing contact retained as negative control without evasion scope");
contactMemory.ChangePointer=true;Check(!EvasionContactProbe.TryRead(contactMemory,contactState,10.1,0,0x400000,0x300000,out _),"changing contact identity rejected");
Check(EvasionContactProbe.FilterCandidate(0x4002,0,1,4)==true,"native invulnerability branch candidate needs attack and hurt categories");
Check(EvasionContactProbe.FilterCandidate(0x4002,0,0x10,4)==false,"body overlap category is not an attack candidate");
Check(EvasionContactProbe.FilterCandidate(0x4000,0,1,4)==false,"roll existence alone does not meet native invulnerability branch");
Check(EvasionContactProbe.FilterCandidate(0x4002,0,1,0x10)==false,"non-hurt target category rejected by native branch");
Check(EvasionContactProbe.FilterCandidate(0x4002,0x2000,1,4)==false,"native roll bypass flag is preserved");
Check(EvasionContactProbe.FilterCandidate(2,0x2000,1,4)==true,"bypass flag only applies with roll flag as native branch specifies");
Check(EvasionContactProbe.FilterCandidate(0x4002,null,1,4)==null&&EvasionContactProbe.FilterCandidate(0x4002,0,null,4)==null&&EvasionContactProbe.FilterCandidate(0x4002,0,1,null)==null,"missing native category or source flags stays unknown");
var definitionMemory=new MemoryFixture();definitionMemory.Pointers[0x300080]=0x200000;definitionMemory.Pointers[0x2001A8]=0x100000;definitionMemory.Pointers[0x3000B8]=0x400000;definitionMemory.Pointers[0x4000A0]=0x500000;
Check(EvasionContactProbe.TryDefinition(definitionMemory,0x300000,out var definition,out var definitionOwner)&&definition==0x500000&&definitionOwner==0x100000,"category correlation locates definition and exact native owner");
definitionMemory.Pointers.Remove(0x4000A0);Check(!EvasionContactProbe.TryDefinition(definitionMemory,0x300000,out _,out _),"missing category definition does not fabricate a mask");
var contactEvadeDirector=new EvasionContactDirector();
var qualifyingContact=new EvasionContactCandidate(20,"HuntingHorn",1,53,"common_roll",0,0x4002,12,0,34946,true,0,1,4,true,EvasionId:1){Owner=0x100000};
ActionReaction? Four(EvasionContactDirector director,EvasionContactCandidate value,double start=20)
{
    ActionReaction? result=null;for(int i=0;i<4;i++)result=director.Observe(value.Owner,value.Weapon,value with {Time=start+i*.1,EvasionId=i+1},start+i*.1+.01);return result;
}
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact,20.1)==null&&contactEvadeDirector.Count==1,"single successful action counts once without praise");
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact with {Time=20.2},20.2)==null&&contactEvadeDirector.Count==1,"repeated collision samples cannot increase action count");
Check(Four(new EvasionContactDirector(),qualifyingContact)?.Kind=="native_evade_streak","four different successful actions trigger one batch praise");
contactEvadeDirector.Reset();
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact with {AttackKind=16,TargetKind=16,InvulnerabilityFilterCandidate=false},20.1)==null,"ordinary body contact stays quiet");
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact with {Owner=0x900000},20.1)==null,"queued foreign-owner contact rejected");
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact with {SourceFlags=132096},20.1)==null,"unverified projectile-owner source stays quiet");
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact with {OtherTimer=1},20.1)==null,"other active invulnerability timer stays quiet");
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact with {WindowTimer=0},20.1)==null,"empty window is not evade success");
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact with {AttackKind=null},20.1)==null,"missing native category cannot produce response");
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact,21)==null,"stale delayed contact cannot produce response");
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact with {Set=3,Id=86,Scope="common_roll"},20.1)==null,"unrequested special evasion cannot spoof allowed scope");
Check(!System.Text.Json.JsonSerializer.Serialize(qualifyingContact).Contains("\"Owner\":"),"runtime owner identity is excluded from serialized diagnostic records");
var shellOwnerMemory=new MemoryFixture();shellOwnerMemory.Values[0x60000C]=2;shellOwnerMemory.Values[0x600060]=0x20400;shellOwnerMemory.Pointers[0x600B40]=0x700000;shellOwnerMemory.Values[0x70000C]=2;shellOwnerMemory.Values[0x700060]=34946;
Check(EvasionContactProbe.ProjectileOwner(shellOwnerMemory,0x600000)==true,"native projectile owner route accepts stable active bit2 source");
shellOwnerMemory.Values[0x700060]=1;Check(EvasionContactProbe.ProjectileOwner(shellOwnerMemory,0x600000)==false,"player-owned projectile is rejected");
shellOwnerMemory.Values[0x700060]=34946;shellOwnerMemory.Values[0x70000C]=0;Check(EvasionContactProbe.ProjectileOwner(shellOwnerMemory,0x600000)==null,"inactive projectile owner remains unknown");
shellOwnerMemory.Values[0x70000C]=2;shellOwnerMemory.Pointers.Remove(0x600B40);Check(EvasionContactProbe.ProjectileOwner(shellOwnerMemory,0x600000)==null,"missing projectile owner is not assumed hostile");
shellOwnerMemory.Pointers[0x600B40]=0x600000;Check(EvasionContactProbe.ProjectileOwner(shellOwnerMemory,0x600000)==null,"cyclic projectile owner is rejected");
shellOwnerMemory.Pointers[0x600B40]=0x700000;shellOwnerMemory.ChangePointer=true;Check(EvasionContactProbe.ProjectileOwner(shellOwnerMemory,0x600000)==null,"changing projectile owner cannot pass eligibility");
contactEvadeDirector.Reset();Check(Four(contactEvadeDirector,qualifyingContact with {SourceFlags=132096,ProjectileOwnerEligible=true})!=null,"verified monster-owned projectile can produce evade response");
contactEvadeDirector.Reset();Check(Four(contactEvadeDirector,qualifyingContact with {SourceFlags=1024,ProjectileOwnerEligible=true})!=null,"second observed projectile flag variant uses same verified owner route");
contactEvadeDirector.Reset();Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact with {SourceFlags=132096,ProjectileOwnerEligible=false},20.1)==null,"non-hostile projectile proof never produces response");
Check(contactEvadeDirector.Observe(0x100000,"HuntingHorn",qualifyingContact with {SourceFlags=0,ProjectileOwnerEligible=true},20.1)==null,"owner eligibility cannot bypass missing shell classification");
ActionReaction? Push(EvasionContactDirector d,EvasionContactCandidate v,long token,double time,bool speak=true)=>d.Observe(v.Owner,v.Weapon,v with{Time=time,EvasionId=token},time,speak);
var rollingEvades=new EvasionContactDirector();
Push(rollingEvades,qualifyingContact,1,0);Push(rollingEvades,qualifyingContact,2,20);Push(rollingEvades,qualifyingContact,3,40);
Check(Push(rollingEvades,qualifyingContact,4,60)!=null&&rollingEvades.Count==0,"normal weapon inclusive sixty-second window praises fourth action and clears count");
Check(Push(rollingEvades,qualifyingContact,4,60.1)==null&&rollingEvades.Count==0,"same action remains deduplicated after praise");
Push(rollingEvades,qualifyingContact,5,60.2);Push(rollingEvades,qualifyingContact,6,60.3);Push(rollingEvades,qualifyingContact,7,60.4);
Check(Push(rollingEvades,qualifyingContact,8,60.5)!=null&&rollingEvades.Responses==2,"next four actions can praise immediately without cooldown");
rollingEvades.Reset();Push(rollingEvades,qualifyingContact,1,0);Push(rollingEvades,qualifyingContact,2,20);Push(rollingEvades,qualifyingContact,3,40);
Check(Push(rollingEvades,qualifyingContact,4,60.01)==null&&rollingEvades.Count==3,"expired old success does not complete a sixty-second batch");
var bowEvade=qualifyingContact with{Weapon="Bow",Set=3,Id=85,Scope="bow_charge_step"};
rollingEvades.Reset();Push(rollingEvades,bowEvade,1,0);Push(rollingEvades,bowEvade,2,10);Push(rollingEvades,bowEvade,3,20);
Check(Push(rollingEvades,bowEvade,4,30)!=null,"bow uses inclusive thirty-second four-action window");
rollingEvades.Reset();Push(rollingEvades,bowEvade,1,0);Push(rollingEvades,bowEvade,2,10);Push(rollingEvades,bowEvade,3,20);
Check(Push(rollingEvades,bowEvade,4,30.01)==null&&rollingEvades.Count==3,"bow does not borrow normal sixty-second window");
var demonStep=qualifyingContact with{Weapon="DualBlades",Set=3,Id=106,Scope="db_demon_step"};
Check(Four(new EvasionContactDirector(),demonStep)!=null&&EvasionContactDirector.WindowSeconds("DualBlades")==30,"demon step uses same native filter and thirty-second policy");
var demonRush=qualifyingContact with{Weapon="DualBlades",Set=3,Id=94,Scope="db_demon_rush",WindowTimer=0,MotionInvulnerability=2,CollisionFlags=2};
Check(Four(new EvasionContactDirector(),demonRush)!=null,"demon rush can use actual native motion-invulnerability mode with attack contact");
Check(Four(new EvasionContactDirector(),demonRush with{MotionInvulnerability=null})==null,"demon rush action alone cannot be praised without native window proof");
Check(Four(new EvasionContactDirector(),demonRush with{MotionInvulnerability=4})==null,"unsupported motion mode is not native evade invulnerability");
Check(Four(new EvasionContactDirector(),qualifyingContact with{WindowTimer=0,MotionInvulnerability=2})==null,"motion fallback is confined to requested demon rush");
rollingEvades.Reset();for(int i=1;i<=4;i++)Push(rollingEvades,qualifyingContact,i,i*.1,false);
Check(rollingEvades.Count==4&&rollingEvades.Responses==0,"chat occupancy does not discard four successful actions");
Push(rollingEvades,qualifyingContact,5,.5,false);
Check(rollingEvades.TryRespond(qualifyingContact.Owner,qualifyingContact.Weapon,.6)!=null&&rollingEvades.Count==0,"batch resets on actual response rather than while speech is occupied");
Check(Push(rollingEvades,qualifyingContact,5,.7)==null&&rollingEvades.Count==0,"pre-praise action is not reused for next batch");
rollingEvades.Reset();for(int i=1;i<=4;i++)Push(rollingEvades,qualifyingContact,i,i*.1,false);
Check(rollingEvades.TryRespond(qualifyingContact.Owner,qualifyingContact.Weapon,61)==null&&rollingEvades.Count==0,"pending batch expires if speaking is blocked beyond rolling window");
Push(rollingEvades,qualifyingContact,5,62,false);rollingEvades.Synchronize(qualifyingContact.Owner,"Bow",62.1);
Check(rollingEvades.Count==0,"weapon switch clears successful-action count even without new contact");
rollingEvades.Reset();Check(Push(rollingEvades,qualifyingContact,0,0)==null&&rollingEvades.Count==0,"unknown action occurrence never counts collision samples");
var episodeMemory=new MemoryFixture();episodeMemory.Pointers[0x1062C8]=0x100000;episodeMemory.Pointers[0x106240]=0x200000;episodeMemory.Values[0x106248]=100;episodeMemory.Pointers[0x2001A8]=0x300000;episodeMemory.Pointers[0x300000]=0x400000;episodeMemory.Pointers[0x300028]=0x100000;episodeMemory.Values[0x106274]=1;episodeMemory.Values[0x106278]=53;episodeMemory.Float(0x300008,.1f);
var episodes=new EvasionEpisodeTracker();var firstEpisode=episodes.Track(episodeMemory,contactState);episodeMemory.Float(0x300008,.2f);
Check(firstEpisode.Id>0&&episodes.Track(episodeMemory,contactState).Id==firstEpisode.Id,"same active action time progression preserves one occurrence");
episodeMemory.Float(0x300008,.01f);var repeatedEpisode=episodes.Track(episodeMemory,contactState);
Check(repeatedEpisode.Id>firstEpisode.Id,"same action ID re-execution is detected by native active-time reset");
episodeMemory.Values[0x106278]=0;episodes.Track(episodeMemory,contactState with{Id=0});episodeMemory.Values[0x106278]=53;
Check(episodes.Track(episodeMemory,contactState).Id>repeatedEpisode.Id,"leaving and reentering action creates a distinct occurrence");
episodeMemory.Float(0x300008,float.NaN);Check(episodes.Track(episodeMemory,contactState).Id==0,"invalid native action time does not invent occurrences");
episodeMemory.Float(0x300008,.3f);episodeMemory.Pointers[0x300028]=0x900000;
Check(episodes.Track(episodeMemory,contactState).Id==0,"foreign action parent cannot create local occurrence");
episodeMemory.Pointers[0x300028]=0x100000;episodeMemory.Float(0x300008,.4f);var liveEpisode=episodes.Track(episodeMemory,contactState);
Check(episodes.Track(episodeMemory,contactState with{Id=0}).Id==0&&episodes.Track(episodeMemory,contactState).Id==liveEpisode.Id,"stale idle snapshot cannot split a live evade into extra occurrences");
episodes.Reset();Check(episodes.Track(episodeMemory,contactState).Id>liveEpisode.Id,"scene reset does not reuse occurrence serials");
var motionMemory=new MemoryFixture();motionMemory.Pointers[0x1006A8]=0x200000;motionMemory.Pointers[0x300080]=0x200000;motionMemory.Pointers[0x400080]=0x500000;motionMemory.Pointers[0x2001A8]=0x100000;motionMemory.Values[0x200198]=2;motionMemory.Float(0x10E168,0);motionMemory.Float(0x10E160,0);motionMemory.Pointers[0x1062C8]=0x100000;motionMemory.Pointers[0x106260]=0x900000;motionMemory.Values[0x106268]=120;motionMemory.Pointers[0x9002F0]=0xA00000;motionMemory.Pointers[0xA00000]=0xB00000;motionMemory.Pointers[0xA00028]=0x100000;motionMemory.Values[0x106274]=3;motionMemory.Values[0x106278]=94;motionMemory.Values[0xA00010]=0x10002;
var motionState=new NativeActionSample(0x100000,"DualBlades",3,94,10);
Check(EvasionContactProbe.TryRead(motionMemory,motionState,10.1,0,0x400000,0x300000,out var motionContact)&&motionContact?.MotionInvulnerability==2,"motion proof reads only actual action low six bits as native bit extraction");
motionMemory.Pointers[0xA00028]=0x900000;Check(EvasionContactProbe.TryRead(motionMemory,motionState,10.1,0,0x400000,0x300000,out motionContact)&&motionContact?.MotionInvulnerability==null,"foreign motion action cannot provide invulnerability proof");
var readyMemory=new MemoryFixture();ulong readyModule=0x140000000;
readyMemory.Pointers[readyModule+0x50139A0]=0x300000;readyMemory.Pointers[0x300050]=0x400000;readyMemory.Pointers[0x407630]=0x500000;readyMemory.Float(0x500060,200);readyMemory.Float(0x500064,150);
Check(ChatReadiness.IsReady(readyMemory,readyModule,"421810"),"native output readiness reads the local actor without invoking native player methods");
Check(ChatReadiness.IsActorReady(readyMemory,0x400000),"validated sampled actor can supply readiness without a global-chain assumption");
Check(!ChatReadiness.IsActorReady(readyMemory,0),"missing sampled actor is never ready");
readyMemory.Float(0x500064,float.NaN);Check(!ChatReadiness.IsReady(readyMemory,readyModule,"421810"),"invalid player HP keeps game output paused");
Check(!ChatReadiness.IsReady(readyMemory,readyModule,"unknown")&&!ChatReadiness.IsReady(new MemoryFixture(),readyModule,"421810"),"menus/missing data and unsupported revisions stay paused");
Console.WriteLine($"PASS {checks} weapon-state checks; no game/API calls");
}
catch(Exception ex){Console.Error.WriteLine("FAIL: "+ex.Message);Environment.ExitCode=1;}

sealed class MemoryFixture:IReadOnlyLocalMemory
{
    public Dictionary<ulong,ulong> Pointers=new();public Dictionary<ulong,int> Values=new();public int Reads;public bool ChangePointer;private int pointerReads;
    public void Float(ulong at,float value)=>Values[at]=BitConverter.SingleToInt32Bits(value);
    public bool Pointer(ulong at,out ulong value){Reads++;pointerReads++;var ok=Pointers.TryGetValue(at,out value);if(ChangePointer&&pointerReads>1)value+=8;return ok;}
    public bool Int32(ulong at,out int value){Reads++;return Values.TryGetValue(at,out value);}
}









