namespace WhaleCompanion;

public sealed record ActionObservation(string Weapon, int Set, int Id, double Time,ulong Owner=0,bool Entered=false);
public sealed record ActionReaction(string Kind, string Text, string Emotion, float Intensity, string Context)
{
    public string[] RequiredStateFields {get;init;}=[];
}

// Conservative weapon action candidates from research/actions; no hit or counter inference.
public sealed class ActionCompanion
{
    private double nextAllowed,nextSpinAllowed,nextHornSpinAllowed;
    private string? lastKind;
    private int variant;
    public double CooldownSeconds { get; set; } = 18;
    public bool IncludeAllWeapons {get;set;}
    public void Reset() { nextAllowed = nextSpinAllowed = nextHornSpinAllowed = 0; lastKind = null; variant = 0; }
    public ActionReaction? Observe(ActionObservation sample, double now)
    {
        if (!double.IsFinite(now) || !double.IsFinite(sample.Time) || now < sample.Time || now - sample.Time > 1.5) return null;
        if (sample.Set != 3) return null;
        // Spin has its own cooldown so a recent power-charge comment does not swallow it.
        if(IncludeAllWeapons&&sample.Weapon=="Hammer"&&sample.Id==95)
        {
            if(now<nextSpinAllowed)return null;nextSpinAllowed=now+ResponseTuning.Cooldown("weapon",18);
            return new("hammer_spin","转圈圈，转圈圈！本鱼也要转晕啦！","relaxed",.5f,
                "玩家已进入大锤回转动作，跟着可爱地说转圈圈，一句就够。不评价命中、伤害或成功失败。");
        }
        if(IncludeAllWeapons&&sample.Weapon=="HuntingHorn"&&sample.Id==74)
        {
            if(now<nextHornSpinAllowed)return null;nextHornSpinAllowed=now+ResponseTuning.Cooldown("weapon",18);
            return new("horn_spin","转呀转，本鱼跟着你的节奏呦！","relaxed",.5f,
                "已确认进入狩猎笛响音攻击动作，只用转呀转陪伴一句。不评价命中、全段伤害或旋律增益生效；重复段保持安静。");
        }
        if(now<nextAllowed)return null;
        var extended=sample.Weapon!="LongSword"&&IncludeAllWeapons?WeaponActions.Identify(sample.Weapon,sample.Set,sample.Id):null;
        if(sample.Weapon!="LongSword"&&extended==null)return null;
        var kind = extended?.Key??(sample.Id switch { 98 => "special_sheathe", 101 => "iai_slash", 102 => "iai_spirit_slash", 86 => "foresight_slash", _ => null });
        if (kind == null) return null;
        // Repeated callbacks/loops must not become repetitive commentary.
        if (kind == lastKind && now < nextAllowed + ResponseTuning.Thresholds.SameActionExtraCooldown) return null;
        lastKind = kind; nextAllowed = now + ResponseTuning.Cooldown("weapon",CooldownSeconds);
        var alternate = variant++ % 2 != 0;
        var text = kind switch
        {
            "special_sheathe" => alternate ? "收刀啦，我也跟着屏住呼吸。" : "慢慢找节奏，我陪你等这个空隙。",
            "iai_slash" => alternate ? "回气准备好，慢慢接下一刀。" : "先把节奏续上，本鱼陪你等下一刀。",
            "iai_spirit_slash" => alternate ? "这一刀出去了，记得留意接下来的动静。" : "呼——出刀的时候，我也跟着紧张了一下。",
            _ => alternate ? "调整一下位置，我跟着你。" : "别急着追下一刀，先顾好自己。"
        };
        if(extended!=null)text=alternate?"本鱼盯着你的节奏呢，别着急。":"这一招出去了，记得留意怪物接下来的动静。";
        if(kind=="sa_axe_prepare")text="斧强化准备起来啦，本鱼陪你接下一招。";
        if(kind=="scope_prepare")text="瞄准准备起来，本鱼陪你找个好角度。";
        if(kind=="lance_counter_prepare")text="盾先架好，本鱼陪你等这个空隙。";
        if(kind=="reload")text="装填准备中，站稳再上呦。";
        if(kind=="horn_spin")text="转呀转，本鱼跟着你的节奏呦！";
        if(kind=="hammer_spin")text=alternate?"转圈圈——本鱼也要转晕啦！":"转圈圈，转圈圈！本鱼给你数着呢！";
        var context = $"游戏只读事件：武器 {sample.Weapon}，ActionSet=3，ActionId={sample.Id}，候选动作标签={kind}，名称={extended?.Label??kind}，原始名称={extended?.RawName??kind}。社区编号映射尚待实机核对。{(sample.Entered?"当前动作字段已匹配进入":"仅收到动作请求，尚未复核是否执行")}，不知道是否命中、反击成功或怪物状态。用一句中文陪伴玩家，避免重复：{text}";
        if(kind=="scope_prepare")context+=" 只观察到切换会心镜模式的动作，不能确认镜头已经开启、瞄准完成或即将命中。";
        if(kind=="lance_counter_prepare")context+=" 只确认反击架势准备，不说已挡住攻击、反击触发或命中。";
        if(kind=="reload")context+=" 只确认装填动作开始，不知道是否完成或被打断，不能宣称弹药已准备好。";
        if(kind=="horn_spin")context+=" 用转呀转陪伴，不从这个动作推断旋律增益生效。";
        if(kind=="iai_slash")context+=" 小居合的正式名称是居合拔刀斩，常用于回气准备；这不是大居合反击结果。只说衔接或回气准备，不说反击成功、规避成功、大成功，不因命中就夸耀，也不宣称已验证回气buff。";
        if(kind=="hammer_spin")context+=" 玩家正在大锤回转动作，请跟着可爱地说转圈圈；不评价命中或伤害。";
        return new(kind, text, "relaxed", 0.45f, context);
    }
}

