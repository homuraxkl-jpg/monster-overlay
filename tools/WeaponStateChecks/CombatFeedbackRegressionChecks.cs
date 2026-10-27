using WhaleCompanion;

static class CombatFeedbackRegressionChecks
{
    public static void Run(Action<bool,string> check)
    {
        const ulong owner=0x100000;
        CombatFacts F(string weapon)=>new(owner,weapon,new(owner,0x200000,new(weapon,6,250,0,0,0),3,0),new(){["spirit_level"]=3,["phials"]=5,["power_charge"]=1},0);
        PlayerDamageCounterSample C(double t,int total)=>new(0,"owner",owner,0x300000,total,t);
        NativeActionTransition T(string w,int from,int to,double time)=>new(new(owner,w,3,from,time-.01),new(owner,w,3,to,time));
        CombatActionRequest R(string w,int id,double t,CombatFacts f,PlayerDamageCounterSample? c=null)=>new(new(w,3,id,t-.005,owner),f with{Time=t-.005},c);
        var rollingSettings=new SaedResponseSettings{RollingOutputEnabled=true,RollingOutputAttack=100,RollingOutputSeconds=10,RollingOutputCooldown=20};
        var rollingEngine=new RollingOutputDirector(); CombatDamageResult? rollingResult=null;
        for(int i=0;i<=40;i++){double t=i*.25;var value=rollingEngine.Observe(C(t,1000+50*i),F("Hammer") with{Time=t},rollingSettings,t);if(i<40)check(value==null,"rolling output requires full observation window "+i);else rollingResult=value;}
        check(rollingResult is {Key:"rolling_output",Damage:2000,Threshold:450},"rolling output uses configured attack and full-window delta");
        check(rollingEngine.Observe(C(10.25,4000),F("Hammer") with{Time=10.25},rollingSettings,10.25)==null,"rolling output cooldown suppresses repeated praise");
        check(rollingEngine.Observe(C(10.5,4500) with{Source="display_local_candidate"},F("Hammer") with{Time=10.5},rollingSettings,10.5)==null,"rolling output excludes unowned display damage");
        check(rollingEngine.Observe(C(10.75,5000),F("Hammer") with{Time=10.75},rollingSettings,10.75)==null,"source change discards old window");
        rollingSettings.RollingOutputAttack=0; check(rollingEngine.Observe(C(11,5100),F("Hammer") with{Time=11},rollingSettings,11)==null,"unknown panel reference disables output judgement");
        var bowEngine=new CombatDamageDirector(); var bowFacts=F("Bow"); var bowSettings=new SaedResponseSettings();
        check(!bowEngine.Enter(T("Bow",1,75,.1),R("Bow",75,.1,bowFacts),C(.1,1000),bowSettings),"single power shot is silent");
        check(!bowEngine.Enter(T("Bow",1,75,4),R("Bow",75,4,bowFacts),C(4,1000),bowSettings),"two power shots remain silent");
        check(bowEngine.Enter(T("Bow",1,75,8.1),R("Bow",75,8.1,bowFacts),C(8.1,1000),bowSettings),"three power shots within eight seconds open stage evaluation");
        bowEngine.Reset();
        check(!bowEngine.Enter(T("Bow",1,75,4),R("Bow",75,4,bowFacts),C(4,1000),bowSettings),"reset clears power-shot sequence");
        check(!bowEngine.Enter(T("Bow",1,75,8),R("Bow",75,8,bowFacts),C(8,1000),bowSettings),"two power shots after reset remain silent");
        bowEngine.Reset();
        foreach(var t in new[]{1d,5d,9.01})check(!bowEngine.Enter(T("Bow",1,75,t),R("Bow",75,t,bowFacts),C(t,1000),bowSettings),"rolling window evicts old shots: "+t);
        check(bowEngine.Enter(T("Bow",1,75,12),R("Bow",75,12,bowFacts),C(12,1000),bowSettings),"rolling window keeps recent three shots");
        var danceEngine=new CombatDamageDirector(); var danceFacts=F("DualBlades") with{Fields=new(){["demon_mode"]=1}};
        var danceSettings=new SaedResponseSettings();
        check(danceEngine.Enter(T("DualBlades",1,92,.02),R("DualBlades",92,.02,danceFacts),C(0,1000),danceSettings),"dance starts one action window");
        for(int tick=1;tick<=8;tick++)check(danceEngine.Observe(C(tick*.1,1000+tick*100),danceFacts with{Time=tick*.1},tick*.1)==null,"dance multi-hit does not evaluate each hit "+tick);
        danceEngine.Enter(T("DualBlades",92,1,.9),null,C(.9,1800),danceSettings);
        check(danceEngine.Observe(C(1,2800),danceFacts with{Time=1},1)?.Damage==800,"dance evaluates once after action and excludes later hits");
        check(danceEngine.Observe(C(1.1,3000),danceFacts with{Time=1.1},1.1)==null,"dance completed window never repeats verdict");
        var diveEngine=new CombatDamageDirector(); var diveFacts=F("InsectGlaive") with{Fields=new(){["red_extract_timer_raw"]=10}};
        check(diveEngine.Enter(T("InsectGlaive",1,115,.02),R("InsectGlaive",115,.02,diveFacts),C(0,1000),new()),"dive opens stage window");
        diveEngine.Observe(C(.1,1400),diveFacts with{Time=.1},.1);
        diveEngine.Enter(T("InsectGlaive",115,78,.2),null,C(.2,1400),new());
        var diveResult=diveEngine.Observe(C(.3,2400),diveFacts with{Time=.3},.3);
        check(diveResult is {Outcome:"ig_dive_follow",Damage:400},"dive links tornado action but excludes its later damage");
        check(diveEngine.Observe(C(.4,2500),diveFacts with{Time=.4},.4)==null,"dive combo delivers one result");
        var settings=new SaedResponseSettings();var facts=F("LongSword");var engine=new CombatDamageDirector();
        engine.Enter(T("LongSword",1,87,.02),R("LongSword",87,.02,facts),C(0,1000),settings);
        // Reproduce a connecting thrust (176) followed by a wholly missed descent.
        for(int i=1;i<=9;i++)engine.Observe(C(i*.1,1176),facts with{Time=i*.1},i*.1);
        engine.Enter(T("LongSword",90,91,1),R("LongSword",91,1,facts,C(.9,1176)),C(1,1176),settings);
        for(int i=11;i<=19;i++)engine.Observe(C(i*.1,1176),facts with{Time=i*.1},i*.1);
        engine.Enter(T("LongSword",92,1,2),null,C(2,1176),settings);
        var result=engine.Observe(C(2.1,2176),facts with{Time=2.1},2.1);
        check(result is {Good:false,Damage:0},"helm thrust excluded and subsequent damage cannot rescue missed descent");

        engine=new();engine.Enter(T("LongSword",99,101,.02),R("LongSword",101,.02,facts),C(0,1000),settings);
        engine.Observe(C(.1,1000),facts with{Time=.1},.1);
        engine.Enter(T("LongSword",101,84,.2),null,C(.2,1000),settings);
        result=engine.Observe(C(.3,2000),facts with{Time=.3},.3);
        check(result==null&&!engine.Active,"small Iai has no damage verdict to contaminate with following combo");

        facts=F("ChargeBlade");engine=new();engine.Enter(T("ChargeBlade",101,119,.02),R("ChargeBlade",119,.02,facts),C(0,1000),settings);
        engine.Observe(C(.1,1000),facts with{Time=.1},.1);
        engine.Enter(T("ChargeBlade",119,120,.2),null,C(.2,1000),settings);
        engine.Enter(T("ChargeBlade",120,118,.22),null,C(.22,1000),settings);
        engine.Observe(C(.3,1500),facts with{Fields=new(facts.Fields){["phials"]=4},Time=.3},.3);
        engine.Enter(T("ChargeBlade",118,3,.4),null,C(.4,1500),settings);
        result=engine.Observe(C(.5,2500),facts with{Fields=new(facts.Fields){["phials"]=4},Time=.5},.5);
        check(result is {Key:"cb_aed",Good:true,Damage:500},"cancelled SAED uses actual AED branch and excludes next attack");

        engine=new();engine.Enter(T("ChargeBlade",101,117,.02),R("ChargeBlade",117,.02,facts),C(0,1000),settings);
        engine.Enter(T("ChargeBlade",118,109,.1),null,C(.1,1000),settings);
        check(!engine.Active&&engine.Observe(C(.2,3000),facts with{Time=.2},.2)==null,"red shield SHIELD_BUFF cancels preparation rather than emitting a finisher verdict");
        engine=new();engine.Enter(T("ChargeBlade",101,119,.02),R("ChargeBlade",119,.02,facts),C(0,1000),settings);
        engine.Enter(T("ChargeBlade",119,72,.1),null,C(.1,1000),settings);
        check(!engine.Active,"uncommitted SAED cannot survive into ordinary sword combo");
        check(CombatProfiles.Identify("Hammer",3,131,F("Hammer"))?.Key=="hammer_charge3","recorded FESTA7 powered charge three has a response");

        facts=F("LongSword");
        foreach(bool branch in new[]{false,true})
        {
            engine=new();engine.Enter(T("LongSword",79,86,.02),R("LongSword",86,.02,facts),C(0,1000),settings);
            engine.Observe(C(.1,1100),facts with{Time=.1},.1);
            engine.Enter(T("LongSword",86,branch?69:1,.2),null,C(.2,1100),settings);
            result=engine.Observe(C(.3,2100),facts with{Time=.3},.3);
            check(branch?result?.Good==true:result==null,"foresight requires direct roundslash branch, not just damage: "+branch);
        }
    }
}





