using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
namespace WhaleCompanion;

// Local, manual research export: main EXE executable sections only, never heap/chat/config memory.
public static class EvasionCodeResearch
{
    [DllImport("kernel32.dll")] [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReadProcessMemory(nint process,nint address,[Out] byte[] bytes,nuint size,out nuint read);
    private static readonly object exportGate=new();
    public static string Export(string directory,string revision){lock(exportGate)return ExportCore(directory,revision);}
    private static string ExportCore(string directory,string revision)
    {
        if(revision!="421810")throw new InvalidOperationException("Unsupported game revision");
        var module=Process.GetCurrentProcess().MainModule??throw new InvalidOperationException("No module");
        if(!Path.GetFileName(module.FileName).Equals("MonsterHunterWorld.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Not the game process");
        var header=new byte[4096];using(var file=File.OpenRead(module.FileName))file.ReadExactly(header);
        int pe=BitConverter.ToInt32(header,0x3C);
        if(pe<64||pe>2048||BitConverter.ToUInt32(header,pe)!=0x4550)throw new InvalidDataException("Invalid PE header");
        int count=BitConverter.ToUInt16(header,pe+6),optional=BitConverter.ToUInt16(header,pe+20),table=pe+24+optional;
        if(count is <1 or >96||table<0||table+count*40>header.Length)throw new InvalidDataException("Invalid sections");
        var rows=new List<object>();var callers=new List<object>();var focus=new List<object>();int hits=0,unreadable=0;
        for(int section=0;section<count;section++)
        {
            int at=table+section*40;
            if((BitConverter.ToUInt32(header,at+36)&0x20000000)==0)continue;
            uint size=BitConverter.ToUInt32(header,at+8),rva=BitConverter.ToUInt32(header,at+12);
            if(size>128*1024*1024||rva>module.ModuleMemorySize||(ulong)rva+size>(ulong)module.ModuleMemorySize)throw new InvalidDataException("Section outside module");
                        foreach(var range in new (uint Rva,int Size)[]{(0x1190A40,0x93),(0x1190B90,0x1274),(0x118F620,0x1D),(0x118F4B0,0xE8),(0x11ADA10,0x29B4),(0x2077A30,0x137),(0x118FB20,0x4C),(0x1F631C0,0x80),(0x2080810,0x520),(0x20804B0,0x2C0),(0x19A5610,0x31),(0x12DBE50,0xC0),(0x2079E30,0x825),(0x2077CA0,0x80)})
            {
                if(range.Rva<rva||(ulong)range.Rva+(uint)range.Size>(ulong)rva+size)continue;
                var code=new byte[range.Size];
                if(ReadProcessMemory(-1,module.BaseAddress+(int)range.Rva,code,(nuint)code.Length,out var copied)&&copied==(nuint)code.Length)
                    focus.Add(new{rva=range.Rva.ToString("X"),code=Convert.ToHexString(code),purpose="candidate_function_context_not_validated_hook"});
                else unreadable++;
            }
            for(uint offset=0;offset<size;offset+=65536-256)
            {
                int length=(int)Math.Min(65536,size-offset);var buffer=new byte[length];
                if(!ReadProcessMemory(-1,module.BaseAddress+(int)(rva+offset),buffer,(nuint)length,out var read)||read!=(nuint)length){unreadable++;continue;}
                for(int i=16;i+48<=length;i++)
                {
                                        if(buffer[i]==0xE8&&i>=64&&i+192<=length)
                    {
                        long destination=(long)rva+offset+i+5+BitConverter.ToInt32(buffer,i+1);
                        if(destination==0x1190A40&&callers.Count<64)callers.Add(new{rva=(rva+offset+(uint)i-64).ToString("X"),call_offset=64,target_rva="1190A40",code=Convert.ToHexString(buffer.AsSpan(i-64,256)),candidate_only=true});
                    }
                    bool timer=buffer[i]==0xC4&&buffer[i+1]==0x0F&&buffer[i+2]==0&&buffer[i+3]==0;
                    bool gear=buffer[i]==8&&buffer[i+1]==0x26&&buffer[i+2]==1&&buffer[i+3]==0;
                    if(!timer&&!gear)continue;
                    hits++;if(rows.Count<256)rows.Add(new{kind=timer?"timer_displacement":"gear_displacement",rva=(rva+offset+(uint)i-16).ToString("X"),hit_offset=16,code=Convert.ToHexString(buffer.AsSpan(i-16,64))});
                }
            }
        }
        Directory.CreateDirectory(directory);var target=Path.Combine(directory,"evasion-code-xrefs.json");
        File.WriteAllText(target,JsonSerializer.Serialize(new{revision,candidate_only=true,scope="main_exe_executable_sections",hits,unreadable,truncated=hits>rows.Count,rows,callers,focus},new JsonSerializerOptions{WriteIndented=true}));
        return target;
    }
    public static (string Path,int Methods,bool CorpusReady) ExportActions(string directory,string revision,ulong player,string weapon)
    {
        lock(exportGate)
        {
            if(revision!="421810"||!WeaponStateProbe.Supports(weapon))throw new InvalidOperationException("Unsupported action research target");
            var module=Process.GetCurrentProcess().MainModule!;
            if(!Path.GetFileName(module.FileName).Equals("MonsterHunterWorld.exe",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Not game process");
            ulong start=(ulong)module.BaseAddress,end=start+(uint)module.ModuleMemorySize;
            var header=new byte[4096];using(var stream=File.OpenRead(module.FileName))stream.ReadExactly(header);
            int pe=BitConverter.ToInt32(header,0x3C);if(pe<64||pe>2048||BitConverter.ToUInt32(header,pe)!=0x4550)throw new InvalidDataException("Invalid PE");
            int count=BitConverter.ToUInt16(header,pe+6),table=pe+24+BitConverter.ToUInt16(header,pe+20);
            if(count is <1 or >96||table+count*40>4096)throw new InvalidDataException("Invalid sections");
            bool Executable(ulong address,int size)
            {
                if(address<start||address>end||(ulong)size>end-address)return false;
                for(int i=0;i<count;i++){int at=table+i*40;ulong rva=BitConverter.ToUInt32(header,at+12),length=BitConverter.ToUInt32(header,at+8);if((BitConverter.ToUInt32(header,at+36)&0x20000000)!=0&&address>=start+rva&&address+(uint)size<=start+rva+length)return true;}
                return false;
            }
            var memory=new LocalProcessMemory();var rows=new List<object>();
            ulong controller=player+0x61C8;
            var diagnostics=new Dictionary<string,int>();
            void Count(string status){diagnostics[status]=diagnostics.GetValueOrDefault(status)+1;}
            var targets=new List<(int Set,int Id)>{(1,53)};
            for(int id=18;id<=22;id++)targets.Add((3,id));
            if(weapon=="Bow")for(int id=85;id<=88;id++)targets.Add((3,id));
            if(weapon=="DualBlades"){targets.Add((3,94));for(int id=105;id<=109;id++)targets.Add((3,id));}
            foreach(var target in targets)
            {
                if(!ActionCodeProbe.TryLocate(memory,player,target.Set,target.Id,out var action,out var vtable,out var parentMatches,out var status)){Count(status);continue;}
                Count(parentMatches?"parent_matches":"parent_differs_code_only");                for(int slot=5;slot<=8;slot++)
                {
                    if(!memory.Pointer(vtable+(uint)slot*8,out var function)||!Executable(function,1024)){Count("method_not_executable");continue;}
                    var bytes=new byte[1024];if(!ReadProcessMemory(-1,(nint)function,bytes,1024,out var read)||read!=1024){Count("code_read_failed");continue;}
                    if(!ActionCodeProbe.TryLocate(memory,player,target.Set,target.Id,out var after,out var afterVtable,out _,out _)||after!=action||afterVtable!=vtable){Count("action_changed_during_code_read");continue;}
                    rows.Add(new{set=target.Set,id=target.Id,slot,rva=(function-start).ToString("X"),code=Convert.ToHexString(bytes),candidate_only=true});
                }
            }
            
            if(rows.Count>0&&(!memory.Pointer(controller+0x100,out var finalOwner)||finalOwner!=player))throw new InvalidDataException("Controller owner changed");
            // Fixed callees observed in revision 421810 roll Execute; export code, never invoke.
            var helpers=new List<object>();
            foreach(uint rva in new uint[]{0x1F752D0,0x1F5E8C0,0x11C0B60,0x11C31A0,0x11C9FB0,0x11CA350,0x11CA0F0,0x11C9E70,0x11C9D30,0x11CA490,0x11CA570,0x118ECB0,0x122FF80,0x1F62750,0x1F72FD0})
            {
                if(!Executable(start+rva,2048)){Count("helper_not_executable");continue;}
                var bytes=new byte[2048];
                if(!ReadProcessMemory(-1,(nint)(start+rva),bytes,2048,out var read)||read!=2048){Count("helper_read_failed");continue;}
                helpers.Add(new{rva=rva.ToString("X"),code=Convert.ToHexString(bytes),candidate_only=true});
            }
            Directory.CreateDirectory(directory);var path=Path.Combine(directory,"evasion-action-code-"+weapon+".json");
            string corpus;
            try{corpus=ExportCorpus(directory,module,header,table,count);}
            catch(Exception error){corpus="failed_"+error.GetType().Name;}
            File.WriteAllText(path,JsonSerializer.Serialize(new{revision,weapon,scope="main_exe_executable_action_methods_only",success=rows.Count>0,diagnostics,rows,helpers,corpus},new JsonSerializerOptions{WriteIndented=true}));return (path,rows.Count,corpusExported);
        }
    }
    private static bool corpusExported;
    private static string ExportCorpus(string directory,ProcessModule module,byte[] header,int table,int count)
    {
        var root=Path.Combine(directory,"evasion-code-corpus-421810");
        var manifest=Path.Combine(root,"manifest.json");
        if(corpusExported)return manifest;
        // Main EXE read-only executable sections only. Never writable sections or other modules.
        var ranges=new List<(uint Rva,uint Size)>();ulong total=0;
        for(int i=0;i<count;i++)
        {
            int at=table+i*40;uint flags=BitConverter.ToUInt32(header,at+36);
            if((flags&0x20000000)==0||(flags&0x80000000)!=0||System.Text.Encoding.ASCII.GetString(header,at,8).TrimEnd('\0')!=".text")continue;
            uint rva=BitConverter.ToUInt32(header,at+12),size=BitConverter.ToUInt32(header,at+8);
            if(size==0)continue;
            if((ulong)rva+size>(ulong)module.ModuleMemorySize||(total+=size)>128UL*1024*1024)throw new InvalidDataException("Code corpus bounds");
            ranges.Add((rva,size));
        }
        if(ranges.Count==0)throw new InvalidDataException("No read-only code sections");
        // Reuse the complete local corpus after checking every expected section and checksum.
        if(File.Exists(manifest))
        {
            try
            {
                using var saved=JsonDocument.Parse(File.ReadAllText(manifest));var value=saved.RootElement;
                bool valid=value.GetProperty("revision").GetString()=="421810"&&value.GetProperty("scope").GetString()=="main_exe_readonly_text_section_only"&&value.GetProperty("success").GetBoolean()&&value.GetProperty("unreadable").GetInt32()==0;
                var old=value.GetProperty("sections");valid&=old.GetArrayLength()==ranges.Count;
                for(int i=0;valid&&i<ranges.Count;i++)
                {
                    var range=ranges[i];var entry=old[i];var name="code-"+range.Rva.ToString("X")+".bin";
                    valid=entry.GetProperty("rva").GetString()==range.Rva.ToString("X")&&entry.GetProperty("size").GetUInt32()==range.Size&&entry.GetProperty("file").GetString()==name;
                    if(!valid)break;
                    using var input=File.OpenRead(Path.Combine(root,name));valid=input.Length==range.Size&&Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input))==entry.GetProperty("sha256").GetString();
                }
                if(valid){corpusExported=true;return manifest;}
            }
            catch{ /* Incomplete/invalid previous corpus is replaced by a fresh code-only capture. */ }
        }
        Directory.CreateDirectory(root);var sections=new List<object>();int unreadable=0;
        foreach(var range in ranges)
        {
            string name="code-"+range.Rva.ToString("X")+".bin",target=Path.Combine(root,name),partial=target+".partial";
            using(var output=File.Create(partial))
            {
                for(uint offset=0;offset<range.Size;)
                {
                    int length=(int)Math.Min(65536U,range.Size-offset);var bytes=new byte[length];
                    if(!ReadProcessMemory(-1,module.BaseAddress+(int)(range.Rva+offset),bytes,(nuint)length,out var read)||read!=(nuint)length)
                    {unreadable++;throw new IOException("Executable section read incomplete");}
                    output.Write(bytes);offset+=(uint)length;
                }
            }
            File.Move(partial,target,true);
            using var input=File.OpenRead(target);
            sections.Add(new{rva=range.Rva.ToString("X"),size=range.Size,file=name,sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input))});
        }
        File.WriteAllText(manifest+".partial",JsonSerializer.Serialize(new{revision="421810",scope="main_exe_readonly_text_section_only",local_research_only=true,success=true,unreadable,sections},new JsonSerializerOptions{WriteIndented=true}));
        File.Move(manifest+".partial",manifest,true);corpusExported=true;return manifest;
    }
}









