namespace WhaleCompanion;
// World 421810 PLAYER_BASE + 0x50, referenced by existing MAS/mhw-toolkit research.
// No native FindMasterPlayer/GetDti calls during startup, menus or output readiness checks.
public static class ChatReadiness
{
    public static bool IsActorReady(IReadOnlyLocalMemory memory,ulong actor)
    {
        if(!LocalProcessMemory.IsAddress(actor,0x7638)||!memory.Pointer(actor+0x7630,out var component)||!LocalProcessMemory.IsAddress(component,0x68)||!memory.Int32(component+0x60,out var maximumBits)||!memory.Int32(component+0x64,out var currentBits))return false;
        float maximum=BitConverter.Int32BitsToSingle(maximumBits),current=BitConverter.Int32BitsToSingle(currentBits);
        return float.IsFinite(maximum)&&float.IsFinite(current)&&maximum is >0 and <=10000&&current>=0&&current<=maximum&&memory.Pointer(actor+0x7630,out var afterComponent)&&afterComponent==component;
    }
    public static bool IsReady(IReadOnlyLocalMemory memory,ulong module,string revision)
    {
        if(revision!="421810"||!LocalProcessMemory.IsAddress(module,8)||!memory.Pointer(module+0x50139A0,out var root)||!LocalProcessMemory.IsAddress(root,0x58)||!memory.Pointer(root+0x50,out var actor)||!IsActorReady(memory,actor))return false;
        return memory.Pointer(module+0x50139A0,out var afterRoot)&&afterRoot==root&&memory.Pointer(root+0x50,out var afterActor)&&afterActor==actor;
    }
}
