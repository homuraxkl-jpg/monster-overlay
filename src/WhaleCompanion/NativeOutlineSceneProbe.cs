namespace WhaleCompanion;
public sealed record NativeOutlineFilterState(int Line,int TargetId,int SampleType,int Markers,float Red,float Green,float Blue,float Alpha,bool DepthTest);
public sealed record NativeOutlineSceneState(int Lines,int Units,int ReadFailures,NativeOutlineFilterState[] Filters,bool? PalicoHasComponent,int? PalicoFlag,string Note)
{public string Route {get;init;}="MoveLine.Top+48/Unit.Next+30; active linked list";}
// SPL UnitManager/MoveLine/MtArray layouts; every read stays OS-protected. No virtual calls or writes.
public static class NativeOutlineSceneProbe
{
    public static bool IsType(IReadOnlyLocalMemory m,ulong obj,ulong module,string name,int size)
    {
        bool Image(ulong p)=>module>=0x10000&&p>=module&&p-module<0x6000000;
        if(!m.Pointer(obj,out var table)||!Image(table)||!m.Pointer(table+32,out var getter)||!Image(getter)||!m.Pointer(getter,out var packed))return false;
        var code=BitConverter.GetBytes(packed);if(code[0]!=0x48||code[1]!=0x8D||code[2]!=5||code[7]!=0xC3)return false;
        long target=(long)getter+7+BitConverter.ToInt32(code,3);
        if(target<0||!Image((ulong)target)||!m.Int32((ulong)target+0x30,out var raw)||((raw&0x7FFFFF)<<2)!=size||!m.Pointer((ulong)target+8,out var text)||!Image(text))return false;
        var expected=System.Text.Encoding.ASCII.GetBytes(name+"\0");
        for(int i=0;i<expected.Length;i+=8)
        {
            if(!m.Pointer(text+(uint)i,out var chunk))return false;
            var bytes=BitConverter.GetBytes(chunk);for(int j=0;j<Math.Min(8,expected.Length-i);j++)if(bytes[j]!=expected[i+j])return false;
        }
        return m.Pointer(obj,out var after)&&after==table;
    }
    public static bool TryFilter(IReadOnlyLocalMemory m,ulong obj,ulong module,int line,out NativeOutlineFilterState? state)
    {
        state=null;if(!IsType(m,obj,module,"uOutlineFilter",0x270))return false;
        var color=new float[4];for(int i=0;i<4;i++){if(!m.Int32(obj+0x180+(uint)i*4,out var bits))return false;color[i]=BitConverter.Int32BitsToSingle(bits);if(!float.IsFinite(color[i])||color[i]<0||color[i]>32)return false;}
        if(!m.Int32(obj+0x190,out var target)||target<0||target>255||!m.Int32(obj+0x194,out var samples)||samples<0||samples>16||!m.Int32(obj+0x1D0,out var markers)||markers<0||markers>3||!m.Int32(obj+0x1E4,out var depth)||(depth&255)>1||!IsType(m,obj,module,"uOutlineFilter",0x270))return false;
        state=new(line,target,samples,markers,color[0],color[1],color[2],color[3],(depth&255)!=0);return true;
    }
    public static NativeOutlineSceneState Capture(IReadOnlyLocalMemory m,ulong manager,ulong module,PalicoHealthSample? palico,double now,string revision)
    {
        var filters=new List<NativeOutlineFilterState>();int units=0,failures=0,lines=0;bool? has=null;int? flag=null;
        if(revision!="421810")return new(0,0,0,[],null,null,"unsupported revision; no writes");
        if(LocalProcessMemory.IsAddress(manager,0x3E3C)&&m.Int32(manager+0x3E38,out lines)&&lines is >=0 and <=64)
        for(int line=0;line<lines && units<8192;line++)
        {
            ulong moveLine=manager+0x38+(uint)line*0xF8;
            if(!m.Int32(moveLine+0x60,out var header)||!m.Pointer(moveLine+0x48,out var top)){failures++;continue;}
            int count=(int)((uint)header>>16);
            if(count>2048){failures++;continue;}
            var visitedUnits=new HashSet<ulong>();ulong unit=top;bool complete=true;
            while(unit!=0&&visitedUnits.Count<2048&&units<8192)
            {
                if(!LocalProcessMemory.IsAddress(unit,0x40)||!visitedUnits.Add(unit)||!m.Int32(unit+0x10,out var unitLine)||unitLine!=line){failures++;complete=false;break;}units++;
                if(palico==null&&PalicoTypeProbe.IsPalico(m,unit,module)&&PalicoHealthReader.TryRead(m,unit,module,now,revision,out var observedCat))palico=observedCat;
                if(TryFilter(m,unit,module,line,out var filter)&&filter!=null&&filters.Count<8)filters.Add(filter);
                if(!m.Pointer(unit+0x30,out unit)){failures++;complete=false;break;}
            }
            if(unit!=0){complete=false;failures++;}
            if(!m.Int32(moveLine+0x60,out var afterHeader)||afterHeader!=header||!m.Pointer(moveLine+0x48,out var afterTop)||afterTop!=top){complete=false;failures++;}
            if(!complete)filters.RemoveAll(x=>x.Line==line);

        }
        else{lines=0;failures++;}
        if(palico!=null&&now>=palico.Time&&now-palico.Time<=2&&PalicoTypeProbe.IsPalico(m,palico.Actor,module)&&m.Pointer(palico.Actor+0x78,out var component))
        {
            var visited=new HashSet<ulong>();has=false;
            while(component!=0&&visited.Count<64)
            {
                if(!LocalProcessMemory.IsAddress(component,0x80)||!visited.Add(component)){has=null;break;}
                if(IsType(m,component,module,"cpModelOutline",0x80))
                {
                    if(m.Pointer(component+0x30,out var owner)&&owner==palico.Actor&&m.Int32(component+0x78,out var bits)){has=true;flag=bits&255;}else has=null;
                    break;
                }
                if(!m.Pointer(component+8,out component)){has=null;break;}
            }
            if(component!=0&&visited.Count>=64&&has==false)has=null;
            if(!m.Pointer(palico.Actor+0x7F58,out var hp)||hp!=palico.Component||!PalicoTypeProbe.IsPalico(m,palico.Actor,module)){has=null;flag=null;}
        }
        return new(lines,units,failures,filters.ToArray(),has,flag,"Read-only observation. No renderer activation. Palico owner attribution unknown; +78 byte meaning unverified.");
    }
}
