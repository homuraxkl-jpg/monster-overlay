using System.Runtime.InteropServices;
using System.Security.Cryptography;
using SharpPluginLoader.Core.Memory;

namespace WhaleCompanion;

// Query ABI/signature: Mi1ice/MHWI-VisualControllerExtended (MIT).
// Only approved model groups: pot 55000, face 56000..56040 and optional outline 57000/57001/57010..57050. All others forward unchanged.
public sealed class FaceMeshExecutor : IDisposable
{
    public const string ModelHash = "4AF7D138E38BAF53D528FD6E38B59C10BC9C59A89097432B44D2A1426EFC09A7";
    public const string MaterialHash = "E9716C92C557194F83D7309C9B0D9A08288EF3C9613D77F510CA4DA6468A64F9";
    public const string NeutralHash = "98BE1CE67C1E167CE5787C0FE027F0A72D2874CAB01B5F6017429F95701D88A2";
    private const string Signature = "48 89 5C 24 08 48 89 6C 24 10 48 89 74 24 18 57 41 56 41 57 48 83 EC 20 45 0F B7 50 04";
    private const string ModelRelative = "nativePC/otomo/ot000/mod/otomo000/otomo000.mod3";
    private Hook<Query>? hook;
    private int selected = FaceSelection.FirstGroup;
    private int potVisible,outlineVisible;
    public bool OutlineAvailable {get;private set;}
    public string FaceTint {get;private set;}="Original";
    public void SetOutlineVisible(bool visible)=>Volatile.Write(ref outlineVisible,visible?1:0);
    public void SetPotVisible(bool visible) => Volatile.Write(ref potVisible, visible ? 1 : 0);
    private long queries, closedEyeQueries, intermediateQueries;
    public long Queries => Interlocked.Read(ref queries);
    public long ClosedEyeQueries => Interlocked.Read(ref closedEyeQueries);
    public long IntermediateQueries => Interlocked.Read(ref intermediateQueries);
    public string Status { get; private set; } = "Not initialized";
    public bool Ready => hook is { IsEnabled: true };
    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool Query(nint first, nint second, nint queryObject, nint fourth);

    public void Initialize(Action<string> log)
    {
        var game = AppContext.BaseDirectory;
        var model = Path.Combine(game, ModelRelative);
        try
        {
            var modelHash=File.Exists(model)?Hash(model):"";
            OutlineAvailable=modelHash==OutlineSelection.ModelHash;
            if (modelHash != ModelHash && !OutlineAvailable)
            { Status = "Face model not installed; neutral model retained"; log(Status); return; }
            var material = Path.ChangeExtension(model, ".mrl3");
            var materialHash=File.Exists(material)?Hash(material):"";
            FaceTint=materialHash==MaterialHash?"Original":materialHash=="5DB28C6B42A8BA32F8DCBBE3F46191F56B869B4E45450D7882AFB215F76B13FC"?"Soft rosy":materialHash=="E2C0C395169296B32FF9E16E59C471B879024486708D181E2091D8C7C84A4157"?"Rosy":OutlineSelection.Tint(materialHash);
            if (OutlineAvailable ? OutlineSelection.Tint(materialHash)=="Unknown" : materialHash!=MaterialHash&&FaceTint=="Unknown")
            { Status = "Face material mismatch; neutral fallback"; RestoreNeutral(game, model, log); log(Status); return; }
            var matches = PatternScanner.Scan(Signature);
            if (matches.Count != 1)
            {
                Status = $"Face hook signature matches={matches.Count}; neutral fallback";
                RestoreNeutral(game, model, log); log(Status); return;
            }
            hook = Hook.Create<Query>(matches[0], Detour);
            Status = "Face mesh hook ready (game verification pending)";
            log($"FACE_EXECUTOR_READY address=0x{matches[0]:X} groups=56000..56040 pot=55000 outline={OutlineAvailable} tint={FaceTint}");
        }
        catch (Exception ex)
        {
            hook?.Dispose(); hook = null;
            Status = $"Face executor stopped: {ex.GetType().Name}";
            RestoreNeutral(game, model, log); log(Status);
        }
    }

    private static string Hash(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
    private void RestoreNeutral(string game, string model, Action<string> log)
    {
        // Installer supplies a hash-verified fallback. No write to an unrelated model.
        try
        {
            var fallback = Path.Combine(game, "nativePC/plugins/CSharp/WhaleCompanion/face-neutral.mod3");
            if (File.Exists(model) && (Hash(model) == ModelHash || Hash(model)==OutlineSelection.ModelHash) && File.Exists(fallback) && Hash(fallback) == NeutralHash)
            { File.Copy(fallback, model, true); log("FACE_FALLBACK_RESTORED on disk; restart game if already in a save"); }
            else log("FACE_FALLBACK_NOT_WRITTEN: use Install-PotToggle.ps1 -Restore after exiting game");
        }
        catch { log("FACE_FALLBACK_WRITE_FAILED: use Install-PotToggle.ps1 -Restore after exiting game"); }
    }

    public void Set(int group) => Volatile.Write(ref selected,
        group >= FaceSelection.FirstGroup && group < FaceSelection.FirstGroup + FaceSelection.StateCount ? group : FaceSelection.FirstGroup);

    private unsafe bool Detour(nint first, nint second, nint queryObject, nint fourth)
    {
        if (queryObject != 0)
        {
            // Original function reads this ushort immediately; no guessed object traversal.
            var group = *(ushort*)(queryObject + 4);
            if (OutlineAvailable && OutlineSelection.TryEvaluate(group,Volatile.Read(ref outlineVisible)!=0,Volatile.Read(ref potVisible)!=0,Volatile.Read(ref selected),out var outline))return outline;
            if (AccessorySelection.TryEvaluate(group, Volatile.Read(ref potVisible) != 0, out var accessoryVisible)) return accessoryVisible;
            if (FaceSelection.TryEvaluate(group, Volatile.Read(ref selected), out var visible))
            {
                Interlocked.Increment(ref queries);
                if (visible && group == FaceSelection.FirstGroup + FaceSelection.Steps) Interlocked.Increment(ref closedEyeQueries);
                if (visible && FaceSelection.IsIntermediate(group)) Interlocked.Increment(ref intermediateQueries);
                return visible;
            }
        }
        return hook!.Original(first, second, queryObject, fourth);
    }
    public void Dispose() { hook?.Dispose(); hook = null; }
}
