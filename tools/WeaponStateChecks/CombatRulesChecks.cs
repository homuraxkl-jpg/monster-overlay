using WhaleCompanion;

static class CombatRulesChecks
{
    public static void Run(Action<bool,string> check)
    {
        check(CombatReference.EquipmentCount==3704&&CombatReference.MotionCount==657,"catalog counts include all fourteen weapons and ammo");
        foreach(var weapon in WeaponStateProbe.Weapons)
            check(Enumerable.Range(0,8192).Any(row=>CombatReference.Weapon(weapon,row)!=null),"equipment progression exists: "+weapon);
        check(CombatReference.Raw("Charge Blade",26,27,28)==163,"SAED blade phases from original sheet");
        check(CombatReference.Raw("Long Sword",32,33)==125&&CombatReference.Raw("Long Sword",24)*7==175,"Iai and helm use different gauge-dependent MV");
        check(CombatProfiles.Shell(0)==(0,1)&&CombatProfiles.Shell(6)==(2,2)&&CombatProfiles.Shell(10)==(1,1)&&CombatProfiles.Shell(18)==(0,7)&&CombatProfiles.Shell(19)==(2,7)&&CombatProfiles.Shell(20)==null,"shell IDs handle legacy groups and appended Iceborne levels");
        check(CombatReference.Fixed("Gunlance",20,1)==8&&CombatReference.Fixed("Gunlance",22,7)==61,"fixed shell damage comes from level table");
        const ulong owner=0x100000;
        CombatFacts Facts(string weapon,double raw=100,params (string Key,double Value)[] values)=>new(owner,weapon,new(owner,0x200000,new(weapon,6,raw,0,0,0),3,0),values.ToDictionary(x=>x.Key,x=>x.Value),0);
        var cb=Facts("ChargeBlade",80,("phials",5),("shield_timer_raw",30));
        var profile=CombatProfiles.Identify("ChargeBlade",3,119,cb)!;
        double low=CombatProfiles.Estimate(profile,cb,119),high=CombatProfiles.Estimate(profile,cb with{Equipment=cb.Equipment! with{Stats=cb.Equipment.Stats with{Raw=350}}},119);
        check(low>0&&low/3<100&&Math.Abs(high/low-350d/80)<.0001,"beginner threshold scales with equipped raw instead of 400 floor");
        var six=cb with{Fields=new(cb.Fields){["phials"]=6}};
        check(CombatProfiles.Estimate(profile,six,119)>low,"extra phial changes reference automatically");
        var elemental=cb with{Equipment=cb.Equipment! with{Stats=cb.Equipment.Stats with{Special=1,ElementType=1,Element=40}}};
        check(CombatProfiles.Estimate(profile,elemental,119)>low,"element phials use elemental reference");
        check(CombatProfiles.Identify("ChargeBlade",4,119,cb)==null&&CombatProfiles.Identify("Bow",3,91,Facts("Bow"))==null,"sets and ordinary coatings are not successful attacks");
        var hammer=Facts("Hammer",100,("power_charge",0));
        var charged=hammer with{Fields=new(){["power_charge"]=1}};
        check(CombatProfiles.Estimate(CombatProfiles.Identify("Hammer",3,94,charged)!,charged,94)>CombatProfiles.Estimate(CombatProfiles.Identify("Hammer",3,94,hammer)!,hammer,94),"hammer power charge chooses different attack rows");
        var gun=Facts("LightBowgun",100,("ammo_index_raw",0),("magazine",5));
        var normal=CombatProfiles.Identify("LightBowgun",3,72,gun)!;
        var sticky=gun with{Fields=new(){["ammo_index_raw"]=13,["magazine"]=5}};
        check(normal==null&&CombatProfiles.Identify("LightBowgun",3,72,sticky)==null,"ordinary ammo does not open damage evaluation windows");
        check(CombatProfiles.Identify("LightBowgun",3,72,gun with{Fields=new(){["ammo_index_raw"]=32,["magazine"]=5}})==null,"recovery/utility ammo not mocked as low damage");
        check(CombatProfiles.Identify("HeavyBowgun",3,119,Facts("HeavyBowgun"))==null,"return-to-idle reload is not a shooting finisher");

        var settings=new SaedResponseSettings();
        PlayerDamageCounterSample Counter(double time,int total)=>new(0,"主人",0x300000,0x400000,total,time);
        (CombatDamageDirector Engine,NativeActionTransition Transition,CombatActionRequest Request) Setup(string weapon,int id,CombatFacts facts)
        {
            var transition=new NativeActionTransition(new(owner,weapon,3,0,0),new(owner,weapon,3,id,.02));
            return(new(),transition,new(new(weapon,3,id,.01,owner),facts with{Time=.01}));
        }
        CombatDamageResult? Finish(CombatDamageDirector engine,CombatFacts facts,Func<double,CombatFacts>? update=null,int total=2000)
        {
            CombatDamageResult? result=null;
            for(int i=1;i<=90;i++){double now=i*.1;var next=(update?.Invoke(now)??facts) with{Time=now};result=engine.Observe(Counter(now,now>=.2?total:1000),next,now)??result;}
            return result;
        }
        var iai=Facts("LongSword",100,("spirit_level",3));
        var setup=Setup("LongSword",102,iai);
        check(!setup.Engine.Enter(setup.Transition,null,Counter(0,1000),settings),"request needs pre-action facts and actual transition");
        check(setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings),"Iai counter window starts with red pre-gauge");
        var good=Finish(setup.Engine,iai);
        check(good?.Good==true&&good.Mechanic.Contains("不降级=True"),"damage plus retained gauge praises Iai counter");
        setup=Setup("LongSword",102,iai);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        var lost=Finish(setup.Engine,iai,t=>iai with{Fields=new(){["spirit_level"]=t<.5?2:3}});
        check(lost==null,"large damage cannot override early gauge loss even if recovered later");
        setup=Setup("LongSword",102,iai with{Fields=new(){["spirit_level"]=0}});
        check(!setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings),"no gauge does not prove successful Iai by staying zero");
        setup=Setup("LongSword",102,iai);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        check(Finish(setup.Engine,iai,t=>iai with{Fields=new()})==null,"unknown post-Iai gauge is silent");
        setup=Setup("LongSword",102,iai);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        check(Finish(setup.Engine,iai,total:1000)==null,"retained gauge without hit damage does not praise");
        setup=Setup("ChargeBlade",120,cb);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        check(Finish(setup.Engine,cb,t=>cb with{Fields=new(cb.Fields){["phials"]=0}})?.Good==true,"SAED damage plus bottle spending");
        setup=Setup("ChargeBlade",120,cb);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        check(Finish(setup.Engine,cb)?.Good==false,"other damage cannot praise unspent cancelled SAED");
        var sa=Facts("SwitchAxe",100);
        setup=Setup("SwitchAxe",124,sa);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        check(Finish(setup.Engine,sa)?.Good==false,"ZSD ticks without finisher not full finish praise");
        setup=Setup("SwitchAxe",124,sa);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        setup.Engine.Enter(new(new(owner,"SwitchAxe",3,124,.03),new(owner,"SwitchAxe",3,125,.04)),null,Counter(0,1000),settings);
        check(Finish(setup.Engine,sa)?.Good==true,"full ZSD finish plus damage praises");
        setup=Setup("SwitchAxe",124,sa);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        setup.Engine.Enter(new(new(owner,"SwitchAxe",3,124,.03),new(owner,"SwitchAxe",3,126,.04)),null,Counter(0,1000),settings);
        check(Finish(setup.Engine,sa)?.Good==false,"early ZSD finish is distinguished even with damage");
        var ls=Facts("LongSword",100,("spirit_level",3));
        setup=Setup("LongSword",87,ls);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        check(Finish(setup.Engine,ls)?.Good==false,"helm thrust damage alone not full helm praise");
        setup=Setup("LongSword",87,ls);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        setup.Engine.Enter(new(new(owner,"LongSword",3,90,.03),new(owner,"LongSword",3,91,.04)),null,Counter(0,1000),settings);
        check(Finish(setup.Engine,ls)?.Good==true,"helm descent plus adequate damage praises");
        setup=Setup("Hammer",81,hammer);setup.Engine.Enter(setup.Transition,setup.Request,Counter(0,1000),settings);
        check(Finish(setup.Engine,hammer,t=>hammer with{Equipment=hammer.Equipment! with{Stats=hammer.Equipment.Stats with{Row=7}}})==null,"equipment switch cancels previous damage instance");
        setup=Setup("Hammer",81,hammer);
        check(!setup.Engine.Enter(setup.Transition,setup.Request with{Before=hammer with{Time=double.NaN}},Counter(0,1000),settings),"nonfinite pre-action facts cannot establish proof");
        var probe=new Memory();probe.Pointers[owner+0x76B0]=0x200000;probe.Integers[0x200000+0x9F8]=9;
        ulong address=owner,nextAddress=0x500000;foreach(uint offset in new uint[]{0x98,0x10,0x70,0x18,0x550}){probe.Pointers[address+offset]=nextAddress;address=nextAddress;nextAddress+=0x10000;}
        probe.Integers[address+0x1D0C]=6;probe.Integers[address+0x20FC]=3;
        check(!EquipmentDamageProbe.TryRead(probe,owner,"ChargeBlade","unsupported",0,out _)&&probe.Reads==0,"equipment version gate precedes memory reads");
        check(EquipmentDamageProbe.TryRead(probe,owner,"ChargeBlade","421810",0,out var equipment)&&equipment!.Stats.Raw==80,"equipped row resolves beginner base stats");
        probe.Integers[address+0x1D0C]=8191;check(!EquipmentDamageProbe.TryRead(probe,owner,"ChargeBlade","421810",0,out _),"unknown row has no endgame fallback");
    }
    sealed class Memory:IReadOnlyLocalMemory
    {
        public Dictionary<ulong,ulong> Pointers=new();public Dictionary<ulong,int> Integers=new();public int Reads;
        public bool Pointer(ulong at,out ulong value){Reads++;return Pointers.TryGetValue(at,out value);}
        public bool Int32(ulong at,out int value){Reads++;return Integers.TryGetValue(at,out value);}
    }
}

