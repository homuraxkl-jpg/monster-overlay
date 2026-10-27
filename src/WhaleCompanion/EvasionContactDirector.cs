namespace WhaleCompanion;

// Rolling successful-action count, never a count of collision samples. No fixed cooldown.
public sealed class EvasionContactDirector
{
    private ulong owner;private string weapon="";private double lastNow=double.NaN;private long lastCountedId;
    private readonly Queue<double> successes=new();
    public int Responses {get;private set;}
    public int Count=>successes.Count;
    public long LastCountedId=>lastCountedId;
    public static int WindowSeconds(string weapon)=>weapon is "Bow" or "DualBlades"?ResponseTuning.Thresholds.BowDualEvasionWindow:ResponseTuning.Thresholds.EvasionWindow;
    public void Reset(){Responses=0;owner=0;weapon="";lastNow=double.NaN;lastCountedId=0;successes.Clear();}
    private void Expire(double now){while(successes.TryPeek(out var time)&&now-time>WindowSeconds(weapon))successes.Dequeue();}
    public void Synchronize(ulong player,string currentWeapon,double now)
    {
        if(player==0||!double.IsFinite(now)){Reset();return;}
        if(owner!=player||weapon!=currentWeapon||double.IsFinite(lastNow)&&now<lastNow)Reset();
        owner=player;weapon=currentWeapon;lastNow=now;Expire(now);
    }
    public static bool Qualifies(EvasionContactCandidate value)
    {
        var scope=EvasionActionScope.Identify(value.Weapon,value.Set,value.Id);
        if(scope==null||value.Scope!=scope||!value.BodyOwnerMatches||value.Result!=0||value.InvulnerabilityFilterCandidate!=true||value.EvasionId<=0)return false;
        bool timed=value.WindowTimer is {} timer&&float.IsFinite(timer)&&timer>0&&timer<=10000;
        bool rushMotion=scope=="db_demon_rush"&&value.WindowTimer==0&&value.MotionInvulnerability is >=1 and <=3;
        if((!timed&&!rushMotion)||value.OtherTimer!=0)return false;
        if(value.SourceFlags is not {} source||!((source&2)!=0||((source&0x400)!=0&&value.ProjectileOwnerEligible==true))||value.AttackKind is not (1 or 3)||value.TargetKind!=4||EvasionContactProbe.FilterCandidate(value.CollisionFlags,value.SourceCollisionFlags,value.AttackKind,value.TargetKind)!=true)return false;
        return true;
    }
    public ActionReaction? Observe(ulong player,string currentWeapon,EvasionContactCandidate value,double now,bool readyToSpeak=true)
    {
        if(player==0||value.Owner!=player||value.Weapon!=currentWeapon||!double.IsFinite(now)||!double.IsFinite(value.Time)||now-value.Time is <0 or >.3)return null;
        Synchronize(player,currentWeapon,now);
        if(!Qualifies(value))return null;
        if(value.EvasionId<=lastCountedId)return null;
        lastCountedId=value.EvasionId;
        // While speech is busy, retain at most one ready batch; reset only when actually delivered.
        if(successes.Count<ResponseTuning.Thresholds.EvasionCount)successes.Enqueue(value.Time);
        return readyToSpeak?TryRespond(player,currentWeapon,now):null;
    }
    public ActionReaction? TryRespond(ulong player,string currentWeapon,double now)
    {
        if(owner!=player||weapon!=currentWeapon||!double.IsFinite(now)||double.IsFinite(lastNow)&&now<lastNow)return null;
        lastNow=now;Expire(now);if(successes.Count<ResponseTuning.Thresholds.EvasionCount)return null;
        successes.Clear();Responses++;
        string text=currentWeapon switch{"Bow"=>"刚才几下躲得真灵活，本鱼都看花眼啦！","DualBlades"=>"刚才几下穿来闪去，本鱼差点跟丢啦！",_=>"刚才几下躲得漂亮，本鱼给你鼓鼓掌！"};
        return new("native_evade_streak",text,"happy",.65f,
            $"在最近{WindowSeconds(currentWeapon)}秒内记录到{ResponseTuning.Thresholds.EvasionCount}次不同实际回避动作，各有原生具体相交、合法来源和无敌过滤证据，当前批次已清空。只夸刚才这几次躲避，不称连续无失误、整场无伤、完美回避或衣装增益。弓和双刀按30秒，其他武器60秒，无额外冷却；不把接触采样数量当成功次数。按人设简短回应。" );
    }
}
