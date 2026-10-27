using System.Text;

namespace WhaleCompanion;

public sealed record NativeLine(long Id, string Text, double Time);

// Bounded local display queue; no network chat API or game pointers here.
public sealed class NativeDialogue
{
    private readonly Queue<NativeLine> lines = new();
    private long sequence;
    private double nextAllowed;
    public int Count=>lines.Count;
    public long LatestId => sequence;
    public int ExpiredCount { get; private set; }
    public void Clear() { lines.Clear(); nextAllowed = 0; }
    public void Enqueue(string text, double now)
    {
        if (!double.IsFinite(now)) return;
        var clean = Format(text);
        if (clean.Length == 0) return;
        lines.Clear();
        lines.Enqueue(new(++sequence, clean, now));
    }
    public NativeLine? Take(double now, bool ready)
    {
        if (!double.IsFinite(now)) { Clear(); return null; }
        while (lines.TryPeek(out var head) && (now < head.Time || now - head.Time > ResponseTuning.Thresholds.EventFreshnessSeconds)){lines.Dequeue();ExpiredCount++;}
        if (!ready) return null; // Scene transitions explicitly clear; transient HUD absence should not drop replies.
        if (now < nextAllowed || !lines.TryDequeue(out var line)) return null;
        nextAllowed = now + ResponseTuning.Thresholds.NativeOutputIntervalSeconds;
        return line;
    }
    public static string Format(string text)
    {
        var output = new StringBuilder();
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (count >= 180) { output.Append('…'); break; }
            if (Rune.IsControl(rune) || Rune.IsWhiteSpace(rune))
            { if (output.Length > 0 && output[^1] != ' ') output.Append(' '); continue; }
            // Do not let AI text introduce game formatting/substitution tags.
            output.Append(rune.Value switch { '<' => "＜", '>' => "＞", _ => rune.ToString() });
            count++;
        }
        var body = output.ToString().Trim();
        if(body.StartsWith(SharedChat.Prefix,StringComparison.Ordinal))body=body[SharedChat.Prefix.Length..].TrimStart();
        return body.Length == 0 ? "" : SharedChat.Prefix + body;
    }
}
