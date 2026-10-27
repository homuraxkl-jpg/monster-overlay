using WhaleCompanion;

static class DefensiveReactionChecks
{
    public static void Run(Action<bool,string> check)
    {
        const ulong owner=0x100000;
        CombatFacts Facts(double t,double? hp=100,double gauge=3)=>new(owner,"LongSword",new(owner,0x200000,new("LongSword",6,250,0,0,0),3,t),hp is {} value?new(){["health"]=value,["spirit_level"]=gauge}:new(){["spirit_level"]=gauge},t);
        CombatDamageResult? Run(Func<double,CombatFacts> observations,CombatFacts? before=null,int damage=0)
        {
            var director=new CombatDamageDirector();var facts=before??Facts(.01);
            var transition=new NativeActionTransition(new(owner,"LongSword",3,99,0),new(owner,"LongSword",3,102,.02));
            director.Enter(transition,new(new("LongSword",3,102,.015,owner),facts),new(0,"owner",owner,0x300000,1000,0),new());
            CombatDamageResult? result=null;
            for(int i=1;i<=35;i++){double t=i*.1;result=director.Observe(new(0,"owner",owner,0x300000,1000+damage,t),observations(t),t)??result;}
            return result;
        }
        var safe=Run(t=>Facts(t));
        check(safe is {Outcome:"iai_safe_exit",Damage:0,Good:false},"unhit retained-blade and unchanged-HP Iai gives safe-exit outcome, not damage praise");
        check(CombatDamageDirector.Reaction(safe!).Kind=="iai_safe_exit"&&!CombatDamageDirector.Reaction(safe!).Text.Contains("空完"),"safe exit has its own non-mocking local and AI reaction");
        check(Run(t=>Facts(t,t<.5?95:100))==null,"damage followed by healing cannot qualify as no-health-loss");
        check(Run(t=>Facts(t,gauge:t<.5?2:3))==null,"early gauge loss followed by recovery is not safe-exit praise");
        check(Run(t=>Facts(t,null),Facts(.01,null))==null,"unknown health cannot qualify as successful evasion");
        check(Run(t=>Facts(t,t is >.2 and <.4?null:100))==null,"health read gap prevents evasion claim");
        check(Run(t=>Facts(t),damage:500)?.Good==true,"landed Iai counter keeps existing praise");
        check(CombatProfiles.Identify("LongSword",3,101,Facts(0))==null,"small Iai is excluded from damage verdicts");
        var chatter=new ActionCompanion();var small=chatter.Observe(new("LongSword",3,101,0,owner,true),0);
        check(small?.Kind=="iai_slash"&&small.Context.Contains("不是大居合反击结果"),"small Iai AI context distinguishes preparation from counter success");
        chatter=new(){IncludeAllWeapons=true};var spin=chatter.Observe(new("Hammer",3,95,0,owner,true),0);
        check(spin is {Kind:"hammer_spin"}&&spin.Text.Contains("转圈圈")&&spin.Context.Contains("不评价命中"),"hammer spin has dedicated playful chatter without hit claims");
        check(chatter.Observe(new("Hammer",3,95,.1,owner,true),.1)==null,"spin loop does not spam every cycle");
        chatter=new(){IncludeAllWeapons=true};chatter.Observe(new("Hammer",3,126,0,owner,true),0);
        check(chatter.Observe(new("Hammer",3,95,1,owner,true),1)?.Kind=="hammer_spin","recent power-charge chatter does not swallow spin companion line");
        var memory=new MemoryFixture();memory.Pointers[owner+0x7630]=0x400000;memory.Float(0x400060,150);memory.Float(0x400064,125);
        check(CombatFacts.Read(memory,owner,"LongSword","421810",0).Value("health")==125,"guarded health reader uses SPL component layout");
        memory.Float(0x400064,float.NaN);
        check(CombatFacts.Read(memory,owner,"LongSword","421810",0).Value("health")==null,"invalid health remains unknown");
    }
}
