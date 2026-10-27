namespace WhaleCompanion;

// Pure state machine: no game pointers or network calls.
public sealed class CompanionEvents
{
    readonly PlayerRecoveryEvents recovery=new();
    public float CooldownSeconds = 12;
    private float? previous, previousMax;
    private bool low;
    private double nextAllowed, accumulated, burstStarted;
    public void Reset() { recovery.Reset(); previous = previousMax = null; low = false; accumulated = 0; }
    public string? Observe(float hp, float maximum, double now)
    {
        bool recovered=recovery.Observe(hp,maximum,now);
        if (maximum <= 0 || !float.IsFinite(hp) || !float.IsFinite(maximum)) { Reset(); return null; }
        var before = previous;
        var sameMax = previousMax == maximum;
        previous = hp; previousMax = maximum;
        if (hp / maximum >= (float)ResponseTuning.Thresholds.LowHealthResetRatio) low = false;
        if (before == null || !sameMax) { accumulated = 0; low = hp / maximum <= (float)ResponseTuning.Thresholds.LowHealthRatio; return null; }
        if (hp <= 0) { accumulated = 0; return null; } // Zero HP alone does not establish a faint.
        if (!low && hp / maximum <= (float)ResponseTuning.Thresholds.LowHealthRatio)
        {
            low = true; accumulated = 0;
            if (now < nextAllowed) return null;
            nextAllowed = now + ResponseTuning.Cooldown("health",CooldownSeconds); return "low_health";
        }
        if (hp < before)
        {
            if (accumulated == 0) burstStarted = now;
            accumulated += before.Value - hp;
        }
        if (accumulated > 0 && now - burstStarted >= ResponseTuning.Thresholds.HealthLossWindow)
        {
            var significant = accumulated >= maximum * ResponseTuning.Thresholds.HealthLossRatio;
            accumulated = 0;
            if (significant && now >= nextAllowed) { nextAllowed = now + ResponseTuning.Cooldown("health",CooldownSeconds); return "health_loss"; }
        }
        if(recovered&&now>=nextAllowed){nextAllowed=now+ResponseTuning.Cooldown("health",CooldownSeconds);return "health_recovered";}
        return null;
    }
}


// Result-only recovery observation: no claim about potion, powder, healing source or item completion.
public sealed class PlayerRecoveryEvents
{
    float? previous,maximum;double last=double.NaN,started,gained,nextAllowed;
    public void Reset(){previous=maximum=null;last=double.NaN;started=gained=nextAllowed=0;}
    public bool Observe(float hp,float max,double now)
    {
        if(!float.IsFinite(hp)||!float.IsFinite(max)||hp<0||max<=0||hp>max||!double.IsFinite(now)){Reset();return false;}
        if(double.IsFinite(last)&&now==last)return false;
        bool discontinuity=previous==null||maximum!=max||!double.IsFinite(last)||now<last||now-last>1||previous<=0||hp<=0;
        if(discontinuity){previous=hp;maximum=max;last=now;gained=0;return false;}
        float delta=hp-previous!.Value;previous=hp;last=now;
        if(delta<0||now-started>ResponseTuning.Thresholds.PlayerHealWindow)gained=0;
        if(delta<=0)return false;
        if(gained==0)started=now;
        gained+=delta;
        if(gained<Math.Max(ResponseTuning.Thresholds.PlayerHealMinimum,max*ResponseTuning.Thresholds.PlayerHealRatio))return false;
        gained=0;
        if(now<nextAllowed)return false;
        nextAllowed=now+ResponseTuning.Thresholds.PlayerHealCooldown;return true;
    }
}
