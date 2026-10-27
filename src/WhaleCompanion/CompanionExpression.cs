using System.Text.Json;

namespace WhaleCompanion;

public record CompanionReply(string Text, string Emotion, float Intensity)
{
    public static CompanionReply Parse(string content)
    {
        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            var text = root.GetProperty("text").GetString();
            if (string.IsNullOrWhiteSpace(text)) throw new JsonException();
            var emotion = root.TryGetProperty("emotion", out var e) && e.ValueKind == JsonValueKind.String ? e.GetString() : "neutral";
            var intensity = root.TryGetProperty("intensity", out var i) && i.TryGetSingle(out var value) && float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0.5f;
            return new(text.Trim(), Normalize(emotion), intensity);
        }
        catch { return new(content.TrimStart().StartsWith('{') ? "" : content.Trim(), "neutral", 0); }
    }

    public static string Normalize(string? emotion) => emotion is "happy" or "sad" or "angry" or "relaxed" or "concerned" ? emotion : "neutral";
}

// Produces timed intent; FacePlayback maps it to verified asset samples.
public sealed class CompanionExpression
{
    private readonly Random random = new();
    private double until, nextBlink = 3, blinkPhase = -1, lastTime;
    private int priority;
    private string target = "neutral";
    private float strength;
    private bool healthDanger;
    private float? previousHealthRatio,previousHealthMaximum;
    private double healthSampleTime=double.NaN;
    public bool HealthDanger=>healthDanger;
    public void ObserveHealth(float health,float maximum,double now,bool enabled=true)
    {
        if(!enabled||!double.IsFinite(now)||!float.IsFinite(health)||!float.IsFinite(maximum)||maximum<=0||health<0||health>maximum)
        {ClearHealth();return;}
        bool continuous=previousHealthMaximum==maximum&&double.IsFinite(healthSampleTime)&&now>=healthSampleTime&&now-healthSampleTime<=1;
        if(!continuous){ClearHealth();}
        float ratio=health/maximum;bool wasDanger=healthDanger;
        healthDanger=healthDanger?ratio<ResponseTuning.Thresholds.LowHealthResetRatio:ratio<=ResponseTuning.Thresholds.LowHealthRatio;
        if(wasDanger!=healthDanger){target="neutral";strength=0;priority=0;until=now;}
        if(!healthDanger&&continuous&&previousHealthRatio<ResponseTuning.Thresholds.FullHealthRelaxRatio&&ratio>=ResponseTuning.Thresholds.FullHealthRelaxRatio)
            Offer("relaxed",.65f,3,now,4);
        previousHealthRatio=ratio;previousHealthMaximum=maximum;healthSampleTime=now;
    }
    private void ClearHealth()
    {
        if(healthDanger){target="neutral";strength=0;priority=0;until=0;}
        healthDanger=false;previousHealthRatio=previousHealthMaximum=null;healthSampleTime=double.NaN;
    }
    public string Emotion { get; private set; } = "neutral";
    public float Weight { get; private set; }
    public float Blink { get; private set; }

    public void Offer(string emotion, float intensity, int importance, double now, double duration = 4)
    {
        if (!double.IsFinite(now) || !double.IsFinite(duration) || duration <= 0) return;
        if (healthDanger) return;
        if (now < until && importance < priority) return;
        target = CompanionReply.Normalize(emotion);
        strength = float.IsFinite(intensity) ? Math.Clamp(intensity, 0, 1) : 0;
        priority = importance; until = now + Math.Min(duration, 10);
    }

    public void Update(double now)
    {
        if (!double.IsFinite(now)) return;
        var dt = Math.Clamp(now - lastTime, 0, 0.1); lastTime = now;
        if (now >= until) { target = "neutral"; priority = 0; }
        var effectiveTarget=healthDanger?"sad":target;
        var effectiveStrength=healthDanger ? .9f : strength;
        // Fade the old expression out before changing its identity.
        var desired = Emotion == effectiveTarget ? (effectiveTarget == "neutral" ? 0 : effectiveStrength) : 0;
        Weight += Math.Clamp(desired - Weight, -(float)dt / 0.22f, (float)dt / 0.22f);
        if (Weight <= 0.001f) { Weight = 0; Emotion = effectiveTarget; }
        // Finish a blink through its closed hold even at low/jittery frame rates.
        // Frame progression is bounded so a delayed update cannot skip the entire closure.
        if (Emotion != "neutral" && Weight > 0.05f) PostponeBlink(now + 1);
        if (blinkPhase < 0 && now >= nextBlink) blinkPhase = 0;
        if (blinkPhase >= 0)
        {
            blinkPhase += Math.Min(dt, 0.025);
            if (blinkPhase < 0.07) Blink = Smooth((float)(blinkPhase / 0.07));
            else if (blinkPhase < 0.115) Blink = 1;
            else if (blinkPhase < 0.22) Blink = 1 - Smooth((float)((blinkPhase - 0.115) / 0.105));
            else { Blink = 0; blinkPhase = -1; nextBlink = now + 2.5 + random.NextDouble() * 3; }
        }
        else Blink = 0;
    }
    private static float Smooth(float x) => x * x * (3 - 2 * x);
    public void PostponeBlink(double untilTime) { nextBlink = untilTime; blinkPhase = -1; Blink = 0; }

    public void Reset(double now)
    {
        ClearHealth();
        target = Emotion = "neutral"; Weight = Blink = strength = 0;
        until = lastTime = now; nextBlink = now + 3; blinkPhase = -1; priority = 0;
    }
}
