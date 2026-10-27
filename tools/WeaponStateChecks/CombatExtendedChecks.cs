using WhaleCompanion;

static class CombatExtendedChecks
{
    public static void Run(Action<bool,string> check)
    {
        const ulong player=0x100000;
        CombatFacts F(string weapon,params (string,double)[] fields)=>new(player,weapon,new(player,0x200000,new(weapon,6,100,0,0,0),3,0),fields.ToDictionary(x=>x.Item1,x=>x.Item2),0);
        PlayerDamageCounterSample Counter(double t,int value)=>new(0,"owner",0x300000,0x400000,value,t);
        (CombatDamageDirector,NativeActionTransition,CombatActionRequest) Setup(string weapon,int id,CombatFacts facts)
        {var transition=new NativeActionTransition(new(player,weapon,3,0,0),new(player,weapon,3,id,.02));return(new(),transition,new(new(weapon,3,id,.01,player),facts with{Time=.01}));}
        CombatDamageResult? Finish(CombatDamageDirector director,CombatFacts facts,Func<double,CombatFacts>? update=null)
        {CombatDamageResult? result=null;for(int i=1;i<=90;i++){double t=i*.1;result=director.Observe(Counter(t,t>.15?3000:1000),(update?.Invoke(t)??facts) with{Time=t},t)??result;}return result;}
        var settings=new SaedResponseSettings();
        var sns=F("SwordAndShield");var (engine,transition,request)=Setup("SwordAndShield",126,sns);engine.Enter(transition,request,Counter(0,1000),settings);
        check(Finish(engine,sns)?.Good==false,"early JUST branch without rush completion not perfect-rush praise");
        (engine,transition,request)=Setup("SwordAndShield",126,sns);engine.Enter(transition,request,Counter(0,1000),settings);
        engine.Enter(new(new(player,"SwordAndShield",3,132,.03),new(player,"SwordAndShield",3,134,.04)),null,Counter(0,1000),settings);
        check(Finish(engine,sns)?.Good==true,"completed JUST finisher and damage praises rush");
        (engine,transition,request)=Setup("SwordAndShield",126,sns);engine.Enter(transition,request,Counter(0,1000),settings);
        engine.Enter(new(new(player,"SwordAndShield",3,132,.03),new(player,"SwordAndShield",3,133,.04)),null,Counter(0,1000),settings);
        check(Finish(engine,sns)?.Good==false,"mistimed rush finish not overridden by total damage");
        var gl=F("GunLance",("shells",5));(engine,transition,request)=Setup("GunLance",104,gl);engine.Enter(transition,request,Counter(0,1000),settings);
        check(Finish(engine,gl)?.Good==false,"full burst requires shell spending");
        (engine,transition,request)=Setup("GunLance",104,gl);engine.Enter(transition,request,Counter(0,1000),settings);
        check(Finish(engine,gl,t=>gl with{Fields=new(){["shells"]=0}})?.Good==true,"shell spending and adequate damage praises burst");
        var glProfile=CombatProfiles.Identify("GunLance",3,104,gl)!;
        check(CombatProfiles.Estimate(glProfile,gl,104)==CombatProfiles.Estimate(glProfile,gl with{Equipment=gl.Equipment! with{Stats=gl.Equipment.Stats with{Raw=500}}},104),"shell threshold follows shell level, not raw attack");
        var gun=F("LightBowgun",("ammo_index_raw",0),("magazine",5));(engine,transition,request)=Setup("LightBowgun",72,gun);engine.Enter(transition,request,Counter(0,1000),settings);
        check(Finish(engine,gun)==null,"unspent magazine cannot get shooting praise from unrelated damage");
        (engine,transition,request)=Setup("LightBowgun",72,gun);engine.Enter(transition,request,Counter(0,1000),settings);
        check(Finish(engine,gun,t=>gun with{Fields=new(){["ammo_index_raw"]=0,["magazine"]=4}})==null,"ordinary ammo stays silent even with consumption and damage");
        var ig=F("InsectGlaive",("red_extract_timer_raw",0));(engine,transition,request)=Setup("InsectGlaive",78,ig);engine.Enter(transition,request,Counter(0,1000),settings);
        check(Finish(engine,ig)?.Good==false,"red-extract move not praised without prerequisite extract");
        var states=new WeaponStateCompanion();var snap=new WeaponStateSnapshot("InsectGlaive",player,0x200000,new(){["red_extract_timer_raw"]=10,["white_extract_timer_raw"]=10,["orange_extract_timer_raw"]=0});
        for(int i=0;i<3;i++)states.Observe(snap,i*.1);
        snap=snap with{Fields=new(snap.Fields){["orange_extract_timer_raw"]=10}};
        states.Observe(snap,.3);states.Observe(snap,.4);
        check(states.Observe(snap,.5)?.Kind=="weapon_state_three_extracts","three extracts praise only on actual timer completion");
        states.Reset();snap=new("HuntingHorn",player,0x200000,new(){["song_effect_38_raw"]=0});for(int i=0;i<3;i++)states.Observe(snap,i*.1);
        snap=snap with{Fields=new(){["song_effect_38_raw"]=10}};states.Observe(snap,.3);states.Observe(snap,.4);
        check(states.Observe(snap,.5)?.Kind=="horn_effect_active","actual song buff activation, not performance animation");
    }
}

