using WhaleCompanion;

static class SoloFallbackChecks
{
    public static void Run(Action<bool,string> check)
    {
        var counter=new DisplayedDamageCounter();counter.Reset(3,0x100000,10);
        counter.Add(new(2,10.1,0x500000,100,1));counter.Add(new(3,9.9,0x500000,100,1));counter.Add(new(3,10.1,0x500000,-1,1));counter.Add(new(3,10.1,0,100,1));
        check(counter.Sample(10.2)?.Total==0,"display counter excludes old scene/pre-reset/invalid events");
        counter.Add(new(3,10.2,0x500000,70,1));counter.Add(new(3,10.2,0x500000,30,1));
        var sample=counter.Sample(10.3);
        check(sample?.Total==100&&sample.Source=="display_local_candidate","display source is explicitly approximate, equal-time hits both counted");
        counter.Gap();check(counter.Sample(10.4)==null,"display overflow/gap cannot turn missing damage into miss");
        counter.Reset(4,0x100008,11);check(counter.Sample(11)?.Total==0&&counter.Matches(4,0x100008),"new actor/scene starts fresh display counter");
        var m=new Memory();const ulong module=0x140000000;
        m.P[module+0x51C46B8]=0x100000;m.P[0x100258]=0x200000;m.P[0x200010]=0x300000;m.I[0x306574]=0;
        check(!PlayerDamageCounter.TryPartySize(m,module,"unknown",out _)&&m.Reads==0,"solo fallback version gate before memory");
        check(PlayerDamageCounter.TryPartySize(m,module,"421810",out var size)&&size==0,"native solo count uses dereference-then-offset semantics");
        m.I[0x306574]=4;check(PlayerDamageCounter.TryPartySize(m,module,"421810",out size)&&size==4,"multiplayer count remains distinct from solo");
        m.I[0x306574]=5;check(!PlayerDamageCounter.TryPartySize(m,module,"421810",out _),"invalid party count does not authorize fallback");
        var window=new SaedDamageWindow();var party=new PlayerDamageCounterSample(0,"same",1,2,1000,0);
        window.Start(party,0,new());check(window.Observe(party with{Time=.1,Source="display_local_candidate"},.1)==null&&!window.Active,"source switch cancels damage window even with same identity fields");
        var facts=new CombatFacts(0x100000,"LongSword",new(0x100000,0x200000,new("LongSword",6,100,0,0,0),3,.01),new(){["spirit_level"]=3},.01);
        var director=new CombatDamageDirector();var before=new PlayerDamageCounterSample(0,"same",1,2,1000,0);
        var request=new CombatActionRequest(new("LongSword",3,102,.01,0x100000),facts,before);
        var transition=new NativeActionTransition(new(0x100000,"LongSword",3,0,0),new(0x100000,"LongSword",3,102,.02));
        check(director.Enter(transition,request,before with{Total=2000,Time=.02},new()),"pre-request counter retained when first hit already entered latest total");
        CombatDamageResult? result=null;
        for(int i=1;i<=35;i++)result=director.Observe(before with{Total=2000,Time=i*.1},facts with{Time=i*.1},i*.1)??result;
        check(result?.Good==true&&result.Damage==1000,"first-frame damage not lost by polling before actual-action observation");
    }
    sealed class Memory:IReadOnlyLocalMemory
    {
        public Dictionary<ulong,ulong>P=new();public Dictionary<ulong,int>I=new();public int Reads;
        public bool Pointer(ulong at,out ulong value){Reads++;return P.TryGetValue(at,out value);}
        public bool Int32(ulong at,out int value){Reads++;return I.TryGetValue(at,out value);}
    }
}
