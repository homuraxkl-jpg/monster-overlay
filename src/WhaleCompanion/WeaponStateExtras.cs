namespace WhaleCompanion;

public static partial class WeaponStateProbe
{
    // HunterPie-legacy 612ef2b, MIT, Player.GetJobInformation/Get*Information.
    // Its weaponAddress is reached through a DIFFERENT chain and includes +236C.
    // Never assume that address is the same as SPL CurrentWeapon +236C.
    private static bool Chain(IReadOnlyLocalMemory m,ulong start,uint[] offsets,out ulong value)
    {
        value=start;
        foreach(var offset in offsets)
        {
            if(!LocalProcessMemory.IsAddress(value,offset+8)||!m.Pointer(value+offset,out value)||!LocalProcessMemory.IsAddress(value,8))return false;
        }
        return true;
    }
    private static bool F(IReadOnlyLocalMemory m,ulong root,uint at,string name,Dictionary<string,double> fields,float max=1000000)
    {
        if(!LocalProcessMemory.IsAddress(root,at+4)||!m.Int32(root+at,out var bits))return false;
        var f=BitConverter.Int32BitsToSingle(bits);
        if(!float.IsFinite(f)||f<0||f>max)return false;
        fields[name]=f;return true;
    }
    private static bool I(IReadOnlyLocalMemory m,ulong root,uint at,string name,Dictionary<string,double> fields,int max=1000,int min=0)
    {
        if(!LocalProcessMemory.IsAddress(root,at+4)||!m.Int32(root+at,out var v)||v<min||v>max)return false;
        fields[name]=v;return true;
    }
    private static bool B(IReadOnlyLocalMemory m,ulong root,uint at,string name,Dictionary<string,double> fields)
    {
        if(!LocalProcessMemory.IsAddress(root,at+4)||!m.Int32(root+at,out var v)||(v&255)>1)return false;
        fields[name]=v&255;return true;
    }
    private static bool ReadLegacy(IReadOnlyLocalMemory m,ulong player,string weapon,Dictionary<string,double> fields)
    {
        if(!Chain(m,player,[0x98,0x10,0x70,0x18,0x550],out var root)||!LocalProcessMemory.IsAddress(root,0x2900))return false;
        // All offsets below are normalized to the legacy weapon OBJECT, not its +236C anchor.
        bool ok=weapon switch
        {
            "GreatSword"=>F(m,root,0x2350,"charge_timer_raw",fields)&&I(m,root,0x2358,"charge_level_raw",fields,4),
            "Hammer"=>B(m,root,0x2354,"power_charge",fields)&&I(m,root,0x235C,"charge_level_raw",fields,4)&&F(m,root,0x2358,"charge_progress_raw",fields),
            "GunLance"=>I(m,root,0x2368,"shell_capacity",fields,8)&&I(m,root,0x236C,"shells",fields,8)&&I(m,root,0x2378,"stake_ammo_raw",fields,2)&&F(m,root,0x2428,"blast_stake_loaded_raw",fields)&&F(m,root,0x242C,"blast_stake_duration_raw",fields),
            "Bow"=>F(m,root,0x23B0,"charge_progress_raw",fields)&&I(m,root,0x23B4,"charge_level_raw",fields,5)&&I(m,root,0x23B8,"max_charge_raw",fields,5),
            "LightBowgun"=>I(m,root,0x2848,"special_ammo_raw",fields,3)&&F(m,root,0x284C,"special_ammo_regen_raw",fields)&&Ammo(m,root,fields),
            "HeavyBowgun"=>I(m,root,0x2354,"special_ammo_type_raw",fields,2)&&F(m,root,0x2358,"wyvernheart_raw",fields)&&F(m,root,0x2360,"wyvernsnipe_raw",fields)&&Ammo(m,root,fields),
            "HuntingHorn"=>Horn(m,root,fields),
            // No special persistent gauge: still inspect actual draw state and sharpness.
            "SwordAndShield" or "Lance"=>B(m,root,0xAA1,"weapon_drawn_raw",fields),
            _=>false
        };
        if(!ok)return false;
        B(m,root,0xAA1,"weapon_drawn_raw",fields);
        if(weapon is not ("Bow" or "LightBowgun" or "HeavyBowgun"))
        {I(m,root,0x20F8,"sharpness_raw",fields,10000);I(m,root,0x20FC,"sharpness_level_raw",fields,8);}
        if(weapon=="HeavyBowgun")
        {B(m,root,0x2824,"scope_equipped",fields);F(m,root,0x283C,"scope_zoom_raw",fields,100);}
        if(weapon=="GunLance")
        {
            if(fields["shells"]>fields["shell_capacity"])return false;
            if(Chain(m,player,[0x7D20],out var ab))F(m,ab,0xC20,"wyvern_fire_cooldown_raw",fields);
            if(m.Pointer(root+0x2570,out var stake)&&LocalProcessMemory.IsAddress(stake,0xE24))F(m,stake,0xE20,"blast_stake_attached_timer_raw",fields);
        }
        // Re-resolve every legacy pointer after reading; don't mix weapon instances.
        return Chain(m,player,[0x98,0x10,0x70,0x18,0x550],out var after)&&after==root;
    }
    private static bool Ammo(IReadOnlyLocalMemory m,ulong root,Dictionary<string,double> fields)
    {
        // Equipped ammo index at (236C+454)+8. The barrel field is explicitly unreliable upstream.
        if(!I(m,root,0x27C8,"ammo_index_raw",fields,39))return false;
        uint offset=0x23A0+(uint)fields["ammo_index_raw"]*24;
        return I(m,root,offset+8,"magazine",fields,1000)&&I(m,root,offset+12,"ammo_total_raw",fields,100000)&&I(m,root,offset+16,"magazine_capacity",fields,1000)&&fields["magazine"]<=fields["magazine_capacity"];
    }
    private static bool Horn(IReadOnlyLocalMemory m,ulong root,Dictionary<string,double> fields)
    {
        if(!m.Pointer(root+0x2450,out var count)||count>4)return false;
        fields["note_queue_count"]=count;
        if(!m.Int32(root+0x2440,out var notes))return false;
        for(int i=0;i<4;i++)fields[$"note_slot_{i}_raw"]=(notes>>(8*i))&255;
        if(m.Pointer(root+0x2470,out var songs)&&songs<=3)fields["song_queue_count"]=songs;
        // Song-ID research is offline only: queued songs are not performed effects.
        return true;
    }
    private static void ReadAdditional(IReadOnlyLocalMemory m,ulong player,ulong data,string weapon,Dictionary<string,double> fields)
    {
        foreach(var (key,value) in ItemEffectProbe.Read(m,player))fields[key]=value;
        if(Chain(m,player,[0x7D20],out var abnormality))
        {
            if(NativeGuardSignal.CanGuard(weapon))F(m,abnormality,0x79C,"offensive_guard_timer_raw",fields);
            if(weapon=="SwitchAxe")
            {B(m,abnormality,0x6E5,"power_axe",fields);F(m,abnormality,0x6E8,"power_axe_timer_raw",fields);}
            if(weapon=="ChargeBlade"&&Chain(m,abnormality,[0x10],out var skills)&&m.Int32(skills+0x78+69*24+8,out var level)&&(level&255)<=1)
            {fields["max_phials"]=5+(level&255);fields["full_phials"]=fields["phials"]==fields["max_phials"]?1:0;}
            if(weapon=="HuntingHorn")
                foreach(var offset in new uint[]{0x38,0x3C,0x40,0x44,0x48,0x4C,0x50,0x54,0x58,0x5C,0x60,0x64,0x68,0x80,0x84,0x88,0x8C,0x90,0x94,0x98,0x9C,0xA0,0xA4,0xA8,0xAC,0xB0,0xB4,0xB8,0xBC,0xC0,0xC4,0xC8,0xCC,0xD0,0xD4,0xD8,0xE4,0xEC,0xF0,0xF4,0xF8,0xFC,0x10C,0x110,0x114,0x118})F(m,abnormality,offset,$"song_effect_{offset:X}_raw",fields);
        }
        if(weapon=="InsectGlaive"&&Chain(m,data,[0x2350],out var kinsect))
        {F(m,kinsect,0xA8C,"kinsect_stamina_raw",fields);F(m,kinsect,0x1BA8,"kinsect_power_timer_raw",fields);F(m,kinsect,0x1BAC,"kinsect_spirit_timer_raw",fields);}
    }
}

// World item EFFECTS only. No claim about item consumption or the player who used it.
public static class ItemEffectProbe
{
    public static readonly Dictionary<string,string> Labels=new()
    {
        ["item_effect_dash_juice"]="强走效果",["item_effect_astera_jerky"]="星辰肉干回复速度效果",
        ["item_effect_cool_drink"]="耐热效果",["item_effect_hot_drink"]="耐寒效果",["item_effect_health_regen"]="体力自然回复强化",
        ["item_effect_might_seed"]="怪力之种效果",["item_effect_might_pill"]="怪力药丸效果",
        ["item_effect_demon_powder"]="鬼人粉尘效果",["item_effect_hardshell_powder"]="硬化粉尘效果",
        ["item_effect_demondrug"]="鬼人药效果",["item_effect_mega_demondrug"]="鬼人药大效果",
        ["item_effect_armorskin"]="硬化药效果",["item_effect_mega_armorskin"]="硬化药大效果"
    };
    public static Dictionary<string,double> Read(IReadOnlyLocalMemory m,ulong player)
    {
        var fields=new Dictionary<string,double>();
        if(!LocalProcessMemory.IsAddress(player,0x7D28)||!m.Pointer(player+0x7D20,out var root)||!LocalProcessMemory.IsAddress(root,0x6FC))return fields;
        void Timer(uint offset,string key)
        {
            if(m.Int32(root+offset,out var bits))
            {var t=BitConverter.Int32BitsToSingle(bits);if(float.IsFinite(t)&&t>=0&&t<=1000000)fields[key]=t;}
        }
        Timer(0x690,"item_effect_dash_juice");Timer(0x698,"item_effect_astera_jerky");
        Timer(0x6EC,"item_effect_cool_drink");Timer(0x6F0,"item_effect_hot_drink");Timer(0x6F8,"item_effect_health_regen");
        Timer(0x6C4,"item_effect_demon_powder");Timer(0x6C8,"item_effect_hardshell_powder");
        if(m.Int32(root+0x6A4,out var might)&&might is 0 or 10 or 25&&m.Int32(root+0x6A0,out var timerBits))
        {
            float timer=BitConverter.Int32BitsToSingle(timerBits);
            if(float.IsFinite(timer)&&timer>=0&&timer<=1000000){fields["item_effect_might_seed"]=might==10?timer:0;fields["item_effect_might_pill"]=might==25?timer:0;}
        }
        void Permanent(uint offset,string normal,string mega)
        {
            if(m.Int32(root+offset,out var level)&&level is >=0 and <=2)
            {fields[normal]=level==1?1:0;fields[mega]=level==2?1:0;}
        }
        Permanent(0x6D4,"item_effect_demondrug","item_effect_mega_demondrug");Permanent(0x6D8,"item_effect_armorskin","item_effect_mega_armorskin");
        if(!m.Pointer(player+0x7D20,out var after)||after!=root)fields.Clear();
        return fields;
    }
}

// HunterPie legacy sHuntingHornMechanics, normalized to legacy weapon root.
// Queue facts only: never a claim that a melody finished or an echo wave was released.
public static class HornSongQueueProbe
{
    public static Dictionary<string,double> Read(IReadOnlyLocalMemory m,ulong root)
    {
        var fields=new Dictionary<string,double>();
        if(!LocalProcessMemory.IsAddress(root,0x24C0)||!m.Pointer(root+0x2470,out var count)||count>3||!m.Pointer(root+0x24A8,out var first)||first>1000000||!m.Pointer(root+0x24B0,out var played)||played>1000000||!m.Int32(root+0x24B8,out var start)||(start&255)>2)return fields;
        for(uint slot=0;slot<3;slot++)
        {
            if(!m.Int32(root+0x2498+slot*4,out var id)||id is <-1 or >4096)return new();
            fields[$"horn_song_id_slot_{slot}_raw"]=id;
        }
        if(!m.Pointer(root+0x2470,out var afterCount)||afterCount!=count||!m.Pointer(root+0x24A8,out var afterFirst)||afterFirst!=first||!m.Pointer(root+0x24B0,out var afterPlayed)||afterPlayed!=played)return new();
        fields["horn_song_id_first_raw"]=first;fields["horn_play_index_raw"]=played;fields["horn_play_start_raw"]=start&255;
        return fields;
    }
}
