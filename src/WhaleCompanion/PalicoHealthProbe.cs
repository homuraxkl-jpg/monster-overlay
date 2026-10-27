namespace WhaleCompanion;

public sealed record PalicoHealthNumber(int Offset,float Float,int? Integer);
// Diagnostic only: no names, raw buffers, owner assertions, HP writes or responses.
public sealed class PalicoHealthProbe
{
    readonly object gate=new();
    readonly System.Collections.Concurrent.ConcurrentQueue<(ulong Actor,double Time)> pending=new();
    readonly System.Collections.Concurrent.ConcurrentDictionary<ulong,byte> described=new();
    readonly System.Collections.Concurrent.ConcurrentQueue<string> typeDetails=new();
    public bool TryTypeDetail(out string? detail)=>typeDetails.TryDequeue(out detail);
    int queued;long inspected,matched;
    public void Inspect(IReadOnlyLocalMemory memory,ulong actor,ulong module,double now)
    {
        Interlocked.Increment(ref inspected);
        if(!PalicoTypeProbe.IsPalico(memory,actor,module))
        {
            if(described.Count<24&&memory.Pointer(actor,out var table)&&described.TryAdd(table,0))typeDetails.Enqueue(PalicoTypeProbe.Describe(memory,actor,module));
            return;
        }
        Interlocked.Increment(ref matched);Offer(actor,now);
    }
    public void Offer(ulong actor,double now){if(Interlocked.Increment(ref queued)>8){Interlocked.Decrement(ref queued);return;}pending.Enqueue((actor,now));}
    public string? Poll(IReadOnlyLocalMemory memory,ulong module,double now,string revision)
    {
        while(pending.TryDequeue(out var entry)){Interlocked.Decrement(ref queued);if(now-entry.Time is >=0 and <=.5&&PalicoTypeProbe.ObjectSize(memory,entry.Actor,module) is >=0x1000 and <=0x20000 and var size)return Capture(memory,entry.Actor,now,revision,size,module);}
        ulong[] known;lock(gate){known=actors.Where(pair=>now-pair.Value.Last>=1).Select(pair=>pair.Key).ToArray();}
        foreach(var actor in known)if(PalicoTypeProbe.IsPalico(memory,actor,module))
        {
            var value=Capture(memory,actor,now,revision);if(value!=null)return value;
        }
        return null;
    }
    readonly Dictionary<ulong,(int Slot,double Last,Dictionary<int,PalicoHealthNumber> Values)> actors=new();
    int samples;
    public string Status=>$"Palico HP research: callbacks {Interlocked.Read(ref inspected)}, recognized {Interlocked.Read(ref matched)}, actors {actors.Count}, samples {samples}; HP={(LatestHealth==null?"waiting":$"{LatestHealth.Current:0.#}/{LatestHealth.Maximum:0.#}")}; owner unknown";
    public void Reset(){lock(gate){actors.Clear();LatestHealth=null;samples=0;}}
    public PalicoHealthSample? LatestHealth {get;private set;}
    public string? Capture(IReadOnlyLocalMemory memory,ulong actor,double now,string revision,int objectSize=0xB8C0,ulong module=0)
    {
        lock(gate)
        {
            if(revision!="421810"||!double.IsFinite(now))return null;
            bool known=actors.TryGetValue(actor,out var state);
            if(known&&now-state.Last<1||!known&&actors.Count>=2)return null;
            if(!PalicoHealthReader.TryRead(memory,actor,module,now,revision,out var health)){LatestHealth=null;return null;}
            int slot=known?state.Slot:actors.Count+1;
            actors[actor]=(slot,now,new());samples++;LatestHealth=health;
            return System.Text.Json.JsonSerializer.Serialize(new{actor_slot=slot,time=now,route="uOt000+7F58/cpHealthManager+60,+64",current=health!.Current,maximum=health.Maximum,hp="observed",owner="unknown",ai_response=false});
        }
    }
    public static List<PalicoHealthNumber> Candidates(byte[] bytes)
    {
        var result=new List<PalicoHealthNumber>();
        // Exclude model transform/header area. Export only plausible numeric facts, never raw bytes.
        for(int offset=0x1000;offset+4<=Math.Min(bytes.Length,0x20000);offset+=4)
        {
            float value=BitConverter.ToSingle(bytes,offset);int integer=BitConverter.ToInt32(bytes,offset);
            if(float.IsFinite(value)&&value>=1&&value<=5000)result.Add(new(offset,value,null));
            else if(integer>=1&&integer<=5000)result.Add(new(offset,0,integer));
        }
        return result;
    }
}

// User-authorized controlled retest after isolation version accepted quests successfully.
// The earlier network failure remains unattributed.
public static class CombatHookIsolation
{
    public static bool Allowed=>true;
}

// Decode only a tiny whitelisted static DTI getter. Never call native virtual functions.
public static class PalicoTypeProbe
{
    public static string Describe(IReadOnlyLocalMemory m,ulong actor,ulong module)
    {
        bool Image(ulong p)=>module>=0x10000&&p>=module&&p-module<0x6000000;
        if(!m.Pointer(actor,out var table))return "stage=object_unreadable";
        if(!Image(table))return "stage=vtable_outside_image";
        if(!m.Pointer(table+32,out var getter))return "stage=getter_pointer_unreadable";
        if(!Image(getter))return $"stage=getter_outside_image table_rva={table-module:X}";
        if(!m.Pointer(getter,out var code))return $"stage=getter_code_unreadable table_rva={table-module:X}";
        var b=BitConverter.GetBytes(code);string prefix=Convert.ToHexString(b);
        string basic=$"table_rva={table-module:X} getter_rva={getter-module:X} code8={prefix}";
        if(b[0]!=0x48||b[1]!=0x8D||b[2]!=5||b[7]!=0xC3)return "stage=getter_shape_unknown "+basic;
        long target=(long)getter+7+BitConverter.ToInt32(b,3);
        if(target<0||!Image((ulong)target))return "stage=dti_outside_image "+basic;
        if(!m.Pointer((ulong)target+8,out var name)||!Image(name))return "stage=name_pointer_unreadable_or_outside_image "+basic;
        if(!m.Pointer(name,out var text))return "stage=name_unreadable "+basic;
        // Only compiled class-name bytes, not object strings or user names.
        return "stage=name_mismatch "+basic+" type_name8="+Convert.ToHexString(BitConverter.GetBytes(text));
    }
    public static bool IsHealthManager(IReadOnlyLocalMemory m,ulong actor,ulong module)
    {
        bool Image(ulong p)=>module>=0x10000&&p>=module&&p-module<0x6000000;
        if(!m.Pointer(actor,out var table)||!Image(table)||!m.Pointer(table+32,out var getter)||!Image(getter)||!m.Pointer(getter,out var code))return false;
        var b=BitConverter.GetBytes(code);
        if(b[0]!=0x48||b[1]!=0x8D||b[2]!=5||b[7]!=0xC3)return false;
        long target=(long)getter+7+BitConverter.ToInt32(b,3);
        if(target<0||!Image((ulong)target)||!m.Pointer((ulong)target+8,out var name)||!Image(name)||!m.Pointer(name,out var first)||!m.Pointer(name+8,out var second))return false;
        var expected=System.Text.Encoding.ASCII.GetBytes("cpHealthManager\0");
        return first==BitConverter.ToUInt64(expected,0)&&second==BitConverter.ToUInt64(expected,8)&&m.Pointer(actor,out var after)&&after==table;
    }
    public static int ObjectSize(IReadOnlyLocalMemory m,ulong actor,ulong module)
    {
        if(!IsPalico(m,actor,module)||!m.Pointer(actor,out var table)||!m.Pointer(table+32,out var getter)||!m.Pointer(getter,out var code))return 0;
        var b=BitConverter.GetBytes(code);if(b[0]!=0x48||b[1]!=0x8D||b[2]!=5||b[7]!=0xC3)return 0;
        long target=(long)getter+7+BitConverter.ToInt32(b,3);
        if(target<(long)module||target-(long)module>=0x6000000||!m.Int32((ulong)target+0x30,out var packed))return 0;
        int size=(packed&0x7FFFFF)<<2;
        return size is >=0x1000 and <=0x20000?size:0;
    }
    public static bool IsPalico(IReadOnlyLocalMemory m,ulong actor,ulong module)
    {
        bool Image(ulong p)=>module>=0x10000&&p>=module&&p-module<0x6000000;
        if(!LocalProcessMemory.IsAddress(actor,8)||!m.Pointer(actor,out var table)||!Image(table)||!m.Pointer(table+32,out var getter)||!Image(getter)||!m.Pointer(getter,out var code))return false;
        var b=BitConverter.GetBytes(code);ulong dti;
        // 48 8D 05 disp32 C3: return &static_DTI.
        if(b[0]==0x48&&b[1]==0x8D&&b[2]==5&&b[7]==0xC3)
        {
            long target=(long)getter+7+BitConverter.ToInt32(b,3);if(target<0)return false;dti=(ulong)target;
        }
        else return false; // Unknown getter shape is a safe miss, not a reason to invoke it.
        if(!Image(dti)||!m.Pointer(dti+8,out var name)||!Image(name)||!m.Pointer(name,out var text))return false;
        // Live World Palico is uOt000, derived from uOtomo (DTI + current game evidence).
        // Exact names plus null; preview, weapons and accessories remain excluded.
        ulong expected=BitConverter.ToUInt64(new byte[]{(byte)'u',(byte)'O',(byte)'t',(byte)'o',(byte)'m',(byte)'o',0,0});
        ulong actualPalico=BitConverter.ToUInt64(new byte[]{(byte)'u',(byte)'O',(byte)'t',(byte)'0',(byte)'0',(byte)'0',0,0});
        ulong exact=text&0x00FFFFFFFFFFFFFFUL;
        if(exact!=expected&&exact!=actualPalico)return false;
        return m.Pointer(actor,out var after)&&after==table&&m.Pointer(table+32,out var getterAfter)&&getterAfter==getter;
    }
}

public sealed record PalicoHealthSample(ulong Actor,ulong Component,float Current,float Maximum,double Time);
public static class PalicoHealthReader
{
    public static bool TryRead(IReadOnlyLocalMemory m,ulong actor,ulong module,double now,string revision,out PalicoHealthSample? sample)
    {
        sample=null;
        if(revision!="421810"||!double.IsFinite(now)||!PalicoTypeProbe.IsPalico(m,actor,module)||!m.Pointer(actor+0x7F58,out var component)||!LocalProcessMemory.IsAddress(component,0x68)||!PalicoTypeProbe.IsHealthManager(m,component,module))return false;
        if(!m.Int32(component+0x60,out var maxBits)||!m.Int32(component+0x64,out var hpBits))return false;
        float maximum=BitConverter.Int32BitsToSingle(maxBits),hp=BitConverter.Int32BitsToSingle(hpBits);
        if(!float.IsFinite(maximum)||!float.IsFinite(hp)||maximum<1||maximum>10000||hp<0||hp>maximum||!m.Pointer(actor+0x7F58,out var after)||after!=component||!m.Int32(component+0x60,out var afterMax)||afterMax!=maxBits)return false;
        sample=new(actor,component,hp,maximum,now);return true;
    }
}
public sealed class PalicoRecoveryDirector
{
    PalicoHealthSample? last;double nextAllowed;float? baseline;double? recoveryStart;
    public void Reset(){last=null;nextAllowed=0;baseline=null;recoveryStart=null;}
    public bool Observe(PalicoHealthSample? sample,bool enabled)
    {
        if(!enabled||sample==null||!float.IsFinite(sample.Current)||!float.IsFinite(sample.Maximum)||sample.Maximum<=0||sample.Current<0||sample.Current>sample.Maximum||!double.IsFinite(sample.Time)){Reset();return false;}
        var before=last;
        if(before!=null&&sample.Time==before.Time)return false;
        last=sample;
        bool low=sample.Current>0&&sample.Current<sample.Maximum*.75f;
        if(before==null||before.Actor!=sample.Actor||before.Component!=sample.Component||before.Maximum!=sample.Maximum||sample.Time<before.Time||sample.Time-before.Time>15||before.Current<=0||sample.Current<=0)
        {baseline=low?sample.Current:null;recoveryStart=null;return false;}
        if(sample.Current<before.Current||recoveryStart is {} started&&sample.Time-started>10)
        {baseline=low?sample.Current:null;recoveryStart=null;return false;}
        if(baseline==null){if(low)baseline=sample.Current;return false;}
        if(sample.Current<=before.Current)return false;
        recoveryStart??=sample.Time;
        if(sample.Current-baseline.Value<sample.Maximum*.25f)return false;
        baseline=null;recoveryStart=null;
        if(sample.Time<nextAllowed)return false;
        nextAllowed=sample.Time+30;return true;
    }
}

public sealed class PendingRecoveryFeedback
{
    double? offered;
    public void Reset()=>offered=null;
    public void Offer(double now){if(double.IsFinite(now))offered=now;}
    public bool TryTake(double now,bool ready)
    {
        if(offered is not {} time)return false;
        if(!double.IsFinite(now)||now<time||now-time>12){offered=null;return false;}
        if(!ready)return false;
        offered=null;return true;
    }
}
