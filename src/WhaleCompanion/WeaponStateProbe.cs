namespace WhaleCompanion;

public sealed record WeaponStateSnapshot(string Weapon, ulong Owner, ulong Data, Dictionary<string,double> Fields);

// Field references: HunterPie ef654889 (Apache-2.0), World Definitions and 421810 map.
// Community candidates only: no hit claims, game writes, callbacks, or model requests.
public static partial class WeaponStateProbe
{
    public const string SupportedRevision="421810";
    public static readonly string[] Weapons=["GreatSword","SwordAndShield","DualBlades","LongSword","Hammer","HuntingHorn","Lance","GunLance","SwitchAxe","ChargeBlade","InsectGlaive","Bow","HeavyBowgun","LightBowgun"];
    public static bool Supports(string weapon)=>Array.IndexOf(Weapons,weapon)>=0;
    public static bool TryRead(IReadOnlyLocalMemory memory,ulong player,string weapon,string revision,out WeaponStateSnapshot? sample,out string status)
    {
        sample=null;status="Unknown: unsupported weapon or game revision";
        if(revision!=SupportedRevision||!Supports(weapon))return false;
        status="Unknown: weapon data unavailable";
        if(!LocalProcessMemory.IsAddress(player,0x76B8)||!memory.Pointer(player+0x76B0,out var data)||!LocalProcessMemory.IsAddress(data,0x2474))return false;
        if(!memory.Int32(data+0x9F8,out var actualType)||actualType!=Array.IndexOf(Weapons,weapon))
        {status="Unknown: weapon object type does not match local player";return false;}
        var fields=new Dictionary<string,double>();
        bool Float(string name,uint offset,float upper=1000000)
        {
            if(!memory.Int32(data+offset,out var bits))return false;
            var value=BitConverter.Int32BitsToSingle(bits);
            if(!float.IsFinite(value)||value<0||value>upper)return false;
            fields.Add(name,value);return true;
        }
        bool ok=false;
        switch(weapon)
        {
            case "LongSword":
                if(memory.Int32(data+0x2370,out var level)&&level is >=0 and <=3)
                {fields.Add("spirit_level",level);ok=Float("spirit_gauge_raw",0x2368,1000)&&Float("spirit_timer_raw",0x2374)&&Float("iai_regen_raw",0x2378)&&Float("helm_regen_raw",0x2388);}
                break;
            case "DualBlades":
                if(memory.Int32(data+0x2368,out var packed))
                {
                    var demon=packed&255;var arch=(packed>>8)&255;
                    if(demon<=1&&arch<=1){fields.Add("demon_mode",demon);fields.Add("arch_demon_mode",arch);ok=Float("demon_gauge_raw",0x236C,1.1f);}
                }
                break;
            case "ChargeBlade":
                if(memory.Int32(data+0x2374,out var phials)&&phials is >=0 and <=6)
                {
                    fields.Add("phials",phials); // Capacity is unknown: never call 5 phials full.
                    ok=Float("sword_energy_raw",0x2370,1000)&&Float("shield_timer_raw",0x2378)&&Float("sword_timer_raw",0x237C)&&Float("axe_timer_raw",0x2470);
                }
                break;
            case "SwitchAxe":
                ok=Float("switch_gauge_raw",0x2350,1000)&&Float("amp_gauge_raw",0x2360,1000)&&Float("amp_timer_raw",0x2364);
                // Power axe lives in a separate abnormality object; do not substitute amp timer.
                break;
            case "InsectGlaive":
                ok=Float("red_extract_timer_raw",0x2368)&&Float("white_extract_timer_raw",0x236C)&&Float("orange_extract_timer_raw",0x2370);
                break;
        }
        if(weapon is not ("LongSword" or "DualBlades" or "ChargeBlade" or "SwitchAxe" or "InsectGlaive"))
            ok=ReadLegacy(memory,player,weapon,fields);
        if(!ok){status="Unknown: incomplete or out-of-range weapon fields";return false;}
        ReadAdditional(memory,player,data,weapon,fields);
        if(!memory.Pointer(player+0x76B0,out var after)||after!=data){status="Unknown: weapon changed during sample";return false;}
        sample=new(weapon,player,data,fields);status="Community candidate: not yet game-validated; no automatic praise";return true;
    }
}

public sealed class WeaponStateTracker
{
    private ulong owner,data;
    private string weapon="";
    private double last=double.NaN;
    private int count;
    public WeaponStateSnapshot? Current {get;private set;}
    public bool Ready=>count>=3&&Current!=null;
    public void Reset(){owner=data=0;weapon="";last=double.NaN;count=0;Current=null;}
    public void Observe(WeaponStateSnapshot? value,double now)
    {
        if(value==null||!double.IsFinite(now)){Reset();return;}
        if(owner!=value.Owner||data!=value.Data||weapon!=value.Weapon||!double.IsFinite(last)||now<last||now-last>1)Reset();
        if(last==now)return;
        owner=value.Owner;data=value.Data;weapon=value.Weapon;last=now;count++;Current=value;
    }
}
