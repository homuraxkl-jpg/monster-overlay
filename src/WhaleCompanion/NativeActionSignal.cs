namespace WhaleCompanion;

public sealed record NativeActionSample(ulong Owner,string Weapon,int Set,int Id,double Time);
public sealed record NativeActionTransition(NativeActionSample Previous,NativeActionSample Current);

// SPL Entity.ActionController (+61C8), CurrentAction (+AC), Owner (+100).
// Unlike OnPlayerAction, these fields describe the CURRENT state, not a request.
public static class NativeActionSignal
{
    public static bool TryRead(IReadOnlyLocalMemory memory,ulong player,string weapon,string revision,double now,out NativeActionSample? sample)
    {
        sample=null;
        if(revision!="421810"||!WeaponStateProbe.Supports(weapon)||!double.IsFinite(now)||!LocalProcessMemory.IsAddress(player,0x62D0))return false;
        if(!memory.Pointer(player+0x62C8,out var owner)||owner!=player||!memory.Int32(player+0x6274,out var set)||set is <0 or >3||!memory.Int32(player+0x6278,out var id)||id is <0 or >8191)return false;
        if(!memory.Pointer(player+0x76B0,out var weaponData)||!LocalProcessMemory.IsAddress(weaponData,0x9FC)||!memory.Int32(weaponData+0x9F8,out var type)||type!=Array.IndexOf(WeaponStateProbe.Weapons,weapon))return false;
        if(!memory.Int32(player+0x6274,out var afterSet)||afterSet!=set||!memory.Int32(player+0x6278,out var afterId)||afterId!=id||!memory.Pointer(player+0x62C8,out var afterOwner)||afterOwner!=player)return false;
        if(!memory.Pointer(player+0x76B0,out var afterWeapon)||afterWeapon!=weaponData||!memory.Int32(afterWeapon+0x9F8,out var afterType)||afterType!=type)return false;
        sample=new(player,weapon,set,id,now);return true;
    }
    public static bool Confirms(ActionObservation request,NativeActionTransition? transition,double now)=>
        transition is {} t&&request.Owner!=0&&double.IsFinite(now)&&now>=request.Time&&now-request.Time<=.75&&
        t.Current.Owner==request.Owner&&t.Current.Weapon==request.Weapon&&t.Current.Set==request.Set&&t.Current.Id==request.Id&&
        t.Current.Time>=request.Time&&now>=t.Current.Time&&now-t.Current.Time<=.25;
}

public sealed class NativeActionTracker
{
    public NativeActionSample? Current {get;private set;}
    public NativeActionTransition? LatestTransition {get;private set;}
    public void Reset(){Current=null;LatestTransition=null;}
    public NativeActionTransition? Observe(NativeActionSample? sample)
    {
        if(sample==null||!double.IsFinite(sample.Time)){Reset();return null;}
        var old=Current;
        if(old==null||old.Owner!=sample.Owner||old.Weapon!=sample.Weapon||sample.Time<old.Time||sample.Time-old.Time>.25)
        {Reset();Current=sample;return null;}
        if(sample.Time==old.Time)return null;
        Current=sample;
        if(old.Set==sample.Set&&old.Id==sample.Id)return null;
        LatestTransition=new(old,sample);return LatestTransition;
    }
}

public sealed record GuardRecoilCandidate(ulong Owner,string Weapon,int PreviousSet,int PreviousId,int RecoilId,string Severity,bool GpActionCandidate,double Time);
public static class NativeGuardRecoil
{
    public static GuardRecoilCandidate? Identify(NativeActionTransition transition)
    {
        var previous=transition.Previous;var current=transition.Current;
        if(previous.Owner!=current.Owner||previous.Weapon!=current.Weapon||!NativeGuardSignal.CanGuard(current.Weapon)||current.Set!=3)return null;
        if(current.Weapon=="Lance"&&current.Id==82&&previous.Set==3&&previous.Id is 76 or 77)
            return new(current.Owner,current.Weapon,previous.Set,previous.Id,current.Id,"large",false,current.Time);
        if(current.Id is <35 or >40)return null;
        var raw=WeaponActions.Name(current.Weapon,current.Set,current.Id);
        if(raw==null||!raw.StartsWith("DAMAGE_GUARD_",StringComparison.Ordinal)&&!raw.StartsWith("DAMAGE_SGUARD_",StringComparison.Ordinal))return null;
        var severity=((current.Id-35)%3) switch{0=>"small",1=>"medium",_=>"large"};
        bool gpCandidate=current.Weapon=="ChargeBlade"&&previous.Set==3&&previous.Id is 70 or 71 or 106;
        // Even a preceding GP-capable action does not prove the collision was inside its GP window.
        return new(current.Owner,current.Weapon,previous.Set,previous.Id,current.Id,severity,gpCandidate,current.Time);
    }
    public static ActionReaction Reaction(GuardRecoilCandidate value)=>new("guard_recoil_observed","盾先稳住，本鱼跟着你找反击的空隙。","relaxed",.45f,
        $"原生当前动作的实验观察：{value.Weapon}已进入防御反作用候选动作{value.RecoilId}，前一个实际动作{value.PreviousSet}:{value.PreviousId}。只观察到防御反作用动作，不知道攻击来源、精确GP、掉血与反击命中，不把进入防御架势当作挡住攻击。按人设说一句简短陪伴，不编造完美GP或伤害。");
}

// Locate code through a validated local controller list. Parent mismatch is diagnostic only:
// code export is not a claim that an action instance belongs exclusively to this hunter.
public static class ActionCodeProbe
{
    public static bool TryLocate(IReadOnlyLocalMemory m,ulong player,int set,int id,out ulong action,out ulong vtable,out bool parentMatches,out string status)
    {
        action=vtable=0;parentMatches=false;status="invalid_target";
        if(set is <0 or >3||id is <0 or >8191||!LocalProcessMemory.IsAddress(player,0x62D0))return false;
        ulong controller=player+0x61C8;
        if(!m.Pointer(controller+0x100,out var owner)||owner!=player){status="controller_owner_mismatch";return false;}
        ulong list=controller+0x68+(uint)set*0x10;
        if(!m.Pointer(list,out var actions)||!m.Int32(list+8,out var count)){status="list_unreadable";return false;}
        if(count is <1 or >8192||id>=count||!LocalProcessMemory.IsAddress(actions,(uint)count*8)){status="list_count_or_pointer_invalid";return false;}
        if(!m.Pointer(actions+(uint)id*8,out action)||!LocalProcessMemory.IsAddress(action,0x38)){status="action_entry_unreadable";return false;}
        if(!m.Pointer(action,out vtable)||!LocalProcessMemory.IsAddress(vtable,72)){status="vtable_unreadable";return false;}
        parentMatches=m.Pointer(action+0x28,out var parent)&&parent==player;
        if(!m.Pointer(controller+0x100,out var ownerAfter)||ownerAfter!=player||!m.Pointer(list,out var afterList)||afterList!=actions||!m.Int32(list+8,out var afterCount)||afterCount!=count||!m.Pointer(actions+(uint)id*8,out var after)||after!=action){status="identity_changed";return false;}
        status="ok";return true;
    }
}
