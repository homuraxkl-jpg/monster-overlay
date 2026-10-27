using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
namespace WhaleCompanion;

public sealed record OutlineImageSection(uint Rva,uint Flags,byte[] Bytes);
public sealed record OutlineNativeType(string Name,uint DtiRva,uint GetterRva,uint TableRva,int Size);
// Pure metadata discovery. No native virtual calls, live object reads, game writes or hooks.
public static class NativeOutlineLocator
{
    public static List<OutlineNativeType> Find(ulong image,IReadOnlyList<OutlineImageSection> sections)
    {
        var result=new List<OutlineNativeType>();
        bool Executable(ulong value)=>value>=image&&sections.Any(s=>(s.Flags&0x20000000)!=0&&value-image>=s.Rva&&value-image-s.Rva<(ulong)s.Bytes.Length);
        foreach(var type in new[]{"cpModelOutline","uOutlineFilter"})
        {
            var literal=Encoding.ASCII.GetBytes(type+"\0");var names=new HashSet<ulong>();
            foreach(var section in sections.Where(s=>(s.Flags&0x20000000)==0))
            {
                int at=0;while(at<section.Bytes.Length){int index=section.Bytes.AsSpan(at).IndexOf(literal);if(index<0)break;int hit=at+index;names.Add(image+section.Rva+(uint)hit);at=hit+literal.Length;}
            }
            var dti=new Dictionary<ulong,int>();
            foreach(var section in sections.Where(s=>(s.Flags&0x20000000)==0))
            for(int offset=8;offset+0x30<=section.Bytes.Length;offset+=8)
            {
                if(!names.Contains(BitConverter.ToUInt64(section.Bytes,offset)))continue;
                int size=(BitConverter.ToInt32(section.Bytes,offset+0x28)&0x7FFFFF)<<2;
                if(size is >=0x60 and <=0x10000 && (type!="cpModelOutline"||size==0x80))dti[image+section.Rva+(uint)offset-8]=size;
            }
            if(dti.Count>8)continue;
            var getters=new Dictionary<ulong,ulong>();
            foreach(var section in sections.Where(s=>(s.Flags&0x20000000)!=0))
            {
                int offset=0;
                while(offset+8<=section.Bytes.Length)
                {
                    int hit=section.Bytes.AsSpan(offset).IndexOf(new byte[]{0x48,0x8D,0x05});if(hit<0)break;offset+=hit;
                    if(offset+8>section.Bytes.Length)break;
                    if(section.Bytes[offset+7]==0xC3)
                    {
                        long target=(long)(image+section.Rva+(uint)offset)+7+BitConverter.ToInt32(section.Bytes,offset+3);
                        if(target>=0&&dti.ContainsKey((ulong)target))getters[image+section.Rva+(uint)offset]=(ulong)target;
                    }
                    offset+=3;
                }
            }
            foreach(var section in sections.Where(s=>(s.Flags&0x20000000)==0))
            for(int offset=32;offset+8<=section.Bytes.Length;offset+=8)
            {
                ulong getter=BitConverter.ToUInt64(section.Bytes,offset);
                if(!getters.TryGetValue(getter,out var info)||!Executable(BitConverter.ToUInt64(section.Bytes,offset-32)))continue;
                if(result.Count>=8)return result;
                result.Add(new(type,(uint)(info-image),(uint)(getter-image),section.Rva+(uint)offset-32,dti[info]));
            }
        }
        return result;
    }
}
public static class NativeOutlineResearch
{
    [DllImport("kernel32.dll",SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    static extern bool ReadProcessMemory(nint process,nint address,byte[] bytes,nuint size,out nuint read);
    public static string Export(string directory,string revision)
    {
        if(revision!="421810")throw new InvalidDataException("Unsupported revision");
        using var process=Process.GetCurrentProcess();var module=process.MainModule??throw new InvalidDataException();
        var header=new byte[4096];
        byte[] Read(uint rva,int size)
        {
            if(size<0||size>128*1024*1024||(ulong)rva+(uint)size>(ulong)module.ModuleMemorySize)throw new InvalidDataException("Outside executable image");
            var data=new byte[size];if(!ReadProcessMemory(-1,module.BaseAddress+(int)rva,data,(nuint)size,out var read)||read!=(nuint)size)throw new IOException("Protected read failed");return data;
        }
        header=Read(0,4096);int pe=BitConverter.ToInt32(header,0x3C);
        if(pe<0||pe+24>header.Length||BitConverter.ToUInt32(header,pe)!=0x4550)throw new InvalidDataException("Invalid PE");
        int count=BitConverter.ToUInt16(header,pe+6),table=pe+24+BitConverter.ToUInt16(header,pe+20);
        if(count is <1 or >32||table+count*40>header.Length)throw new InvalidDataException("Invalid section table");
        var sections=new List<OutlineImageSection>();long total=0;
        for(int index=0;index<count;index++)
        {
            int at=table+index*40;uint flags=BitConverter.ToUInt32(header,at+36),rva=BitConverter.ToUInt32(header,at+12),size=BitConverter.ToUInt32(header,at+8);
            if((flags&0x40000000)==0||size==0)continue;
            if(size>128*1024*1024||(total+=size)>256*1024*1024)throw new InvalidDataException("Image scan bound exceeded");
            sections.Add(new(rva,flags,Read(rva,(int)size)));
        }
        var types=NativeOutlineLocator.Find((ulong)module.BaseAddress,sections);var functions=new List<object>();var targets=new HashSet<uint>();
        foreach(var type in types)
        {
            // Slots are candidates, not callable APIs. Stop at a non-executable entry.
            for(int slot=0;slot<23;slot++)
            {
                ulong pointer=BitConverter.ToUInt64(Read(type.TableRva+(uint)slot*8,8));
                if(pointer<(ulong)module.BaseAddress||pointer-(ulong)module.BaseAddress>uint.MaxValue)break;
                uint rva=(uint)(pointer-(ulong)module.BaseAddress);var section=sections.FirstOrDefault(s=>(s.Flags&0x20000000)!=0&&rva>=s.Rva&&rva-s.Rva<s.Bytes.Length);
                if(section==null)break;
                int length=Math.Min(512,section.Bytes.Length-(int)(rva-section.Rva));
                functions.Add(new{type=type.Name,slot,rva=rva.ToString("X"),code=Convert.ToHexString(Read(rva,length)),candidate_only=true});targets.Add(rva);
            }
        }
        var callers=new List<object>();
        foreach(var section in sections.Where(s=>(s.Flags&0x20000000)!=0))
        for(int i=32;i+133<=section.Bytes.Length&&callers.Count<64;i++)
        {
            if(section.Bytes[i]!=0xE8)continue;
            long target=(long)section.Rva+i+5+BitConverter.ToInt32(section.Bytes,i+1);
            if(target<0||target>uint.MaxValue||!targets.Contains((uint)target))continue;
            callers.Add(new{rva=(section.Rva+(uint)i-32).ToString("X"),call_offset=32,target_rva=((uint)target).ToString("X"),code=Convert.ToHexString(section.Bytes.AsSpan(i-32,160)),candidate_only=true});
        }
        Directory.CreateDirectory(directory);var destination=Path.Combine(directory,"native-outline-421810-v1.json");
        var json=JsonSerializer.Serialize(new{revision,scope="main_exe_native_outline_code_only",types,functions,callers,note="No heap, player strings, credentials, native calls, hooks or writes. Type metadata and code discovery do not enable an outline effect."},new JsonSerializerOptions{WriteIndented=true});
        File.WriteAllText(destination+".tmp",json);File.Move(destination+".tmp",destination,true);
        return $"Native outline capture: {types.Count} types, {functions.Count} candidate methods, {callers.Count} call sites; no rendering changes";
    }
}
