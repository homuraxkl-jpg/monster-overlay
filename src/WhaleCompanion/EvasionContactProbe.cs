namespace WhaleCompanion;

public sealed record EvasionContactCandidate(double Time,string Weapon,int Set,int Id,string? Scope,int Result,uint CollisionFlags,float? WindowTimer,float? OtherTimer,uint? SourceFlags,bool BodyOwnerMatches,uint? SourceCollisionFlags=null,uint? AttackKind=null,uint? TargetKind=null,bool? InvulnerabilityFilterCandidate=null,bool? ProjectileOwnerEligible=null,long EvasionId=0,int? MotionInvulnerability=null)
{
    [System.Text.Json.Serialization.JsonIgnore] public ulong Owner {get;init;}
}
// Collision callback candidate only. No damage attribution, geometry proof or AI decision.
public static class EvasionContactProbe
{
    // Mirrors only the native 12D6F54..82 bit-mask branch. It is not a success verdict.
    public static bool? FilterCandidate(uint targetFlags,uint? sourceFlags,uint? attackKind,uint? targetKind)
    {
        if(sourceFlags==null||attackKind==null||targetKind==null)return null;
        return (targetFlags&2)!=0&&!((targetFlags&0x4000)!=0&&(sourceFlags.Value&0x2000)!=0)&&(attackKind.Value&0x20003)!=0&&(targetKind.Value&0x4008C)!=0;
    }
    // Same single-owner route used by native mantle source eligibility (2077A30).
    // Unknown/inactive/changing owners remain unknown; player-owned shells do not pass bit2.
    public static bool? ProjectileOwner(IReadOnlyLocalMemory memory,ulong source)
    {
        if(!LocalProcessMemory.IsAddress(source,0xB48)||!memory.Int32(source+0xC,out var active)||(active&0xE)==0||!memory.Int32(source+0x60,out var flags)||((flags&0x400)==0||(flags&0x10)!=0))return null;
        if(!memory.Pointer(source+0xB40,out var parent)||parent==source||!LocalProcessMemory.IsAddress(parent,0x64)||!memory.Int32(parent+0xC,out var parentActive)||(parentActive&0xE)==0||!memory.Int32(parent+0x60,out var parentFlags))return null;
        if(!memory.Pointer(source+0xB40,out var after)||after!=parent||!memory.Int32(source+0x60,out var flagsAfter)||flagsAfter!=flags||!memory.Int32(parent+0x60,out var parentFlagsAfter)||parentFlagsAfter!=parentFlags||!memory.Int32(parent+0xC,out var activeAfter)||(activeAfter&0xE)==0)return null;
        return (parentFlags&2)!=0;
    }
    public static bool TryDefinition(IReadOnlyLocalMemory memory,ulong node,out ulong definition,out ulong owner)
    {
        definition=owner=0;
        return LocalProcessMemory.IsAddress(node,0xC0)&&memory.Pointer(node+0x80,out var collider)&&LocalProcessMemory.IsAddress(collider,0x1B0)&&memory.Pointer(collider+0x1A8,out owner)&&memory.Pointer(node+0xB8,out var parameter)&&LocalProcessMemory.IsAddress(parameter,0xA8)&&memory.Pointer(parameter+0xA0,out definition)&&LocalProcessMemory.IsAddress(definition,0x54);
    }
    public static bool TryRead(IReadOnlyLocalMemory memory,NativeActionSample state,double now,long result,ulong first,ulong second,out EvasionContactCandidate? sample)
    {
        sample=null;
        if(!double.IsFinite(now)||now-state.Time is <0 or >.25||!double.IsFinite(state.Time)||!LocalProcessMemory.IsAddress(state.Owner,0xE16C))return false;
        if(!LocalProcessMemory.IsAddress(first,0x88)||!LocalProcessMemory.IsAddress(second,0x88))return false;
        if(!memory.Pointer(state.Owner+0x6A8,out var body)||!memory.Pointer(second+0x80,out var target)||target!=body||!memory.Pointer(first+0x80,out var attacker)||attacker==body)return false;
        if(!LocalProcessMemory.IsAddress(body,0x1B0)||!LocalProcessMemory.IsAddress(attacker,0x1B0))return false;
        if(!memory.Int32(body+0x198,out var flags))return false;
        float? Timer(ulong address){if(!memory.Int32(address,out var bits))return null;var value=BitConverter.Int32BitsToSingle(bits);return float.IsFinite(value)&&value>=0&&value<=10000?value:null;}
        var window=Timer(state.Owner+0xE168);var other=Timer(state.Owner+0xE160);uint? sourceFlags=null;bool? projectileOwner=null;
        if(memory.Pointer(attacker+0x1A8,out var source)&&memory.Int32(source+0x60,out var bits))
        {
            sourceFlags=unchecked((uint)bits);
            if((bits&0x400)!=0)projectileOwner=ProjectileOwner(memory,source);
            if(!memory.Pointer(attacker+0x1A8,out var sourceAfter)||sourceAfter!=source)return false;
        }
        bool ownerMatches=memory.Pointer(body+0x1A8,out var owner)&&owner==state.Owner;
        if(!memory.Pointer(state.Owner+0x6A8,out var afterBody)||afterBody!=body||!memory.Pointer(second+0x80,out var afterTarget)||afterTarget!=target||!memory.Pointer(first+0x80,out var afterAttacker)||afterAttacker!=attacker)return false;
        int? motionInvulnerability=null;
        // 1BFF6F0 -> current action +10; 161A160 extracts native bit range 0..5.
        if(state.Weapon=="DualBlades"&&state.Set==3&&state.Id==94&&ActionCodeProbe.TryLocate(memory,state.Owner,state.Set,state.Id,out var motionAction,out _,out var motionParent,out _)&&motionParent&&memory.Int32(motionAction+0x10,out var motionFlags)&&memory.Int32(state.Owner+0x6274,out var motionSet)&&motionSet==state.Set&&memory.Int32(state.Owner+0x6278,out var motionId)&&motionId==state.Id)
            motionInvulnerability=motionFlags&0x3F;
        uint? attackerFlags=memory.Int32(attacker+0x198,out var attackFlags)?unchecked((uint)attackFlags):null;
        sample=new(now,state.Weapon,state.Set,state.Id,EvasionActionScope.Identify(state.Weapon,state.Set,state.Id),unchecked((int)result),unchecked((uint)flags),window,other,sourceFlags,ownerMatches,attackerFlags,ProjectileOwnerEligible:projectileOwner,MotionInvulnerability:motionInvulnerability){Owner=state.Owner};return true;
    }
}
