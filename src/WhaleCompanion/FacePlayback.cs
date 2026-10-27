namespace WhaleCompanion;

// Manual previews and AI/local intent share the same presentation path.
public sealed class FacePlayback
{
    private string preview = "", fromEmotion = "neutral";
    private double previewStart, previewEnd, ramp = 0.22, lastTime;
    private float fromWeight, fromBlink;
    private bool blinking, neutralPresented;
    private float peak;
    private int closedFrames;
    public string Emotion { get; private set; } = "neutral";
    public float Weight { get; private set; }
    public float Happy => Emotion == "happy" ? Weight : 0;
    public float Blink { get; private set; }
    public bool ManualActive { get; private set; }
    public int Group => FaceSelection.Select(Emotion,Weight,Blink);
    public int CompletedBlinks { get; private set; }
    public float LastBlinkPeak { get; private set; }
    public int LastClosedFrames { get; private set; }
    private static float Ease(double t) { var x=(float)Math.Clamp(t,0,1);return x*x*(3-2*x); }
    private static float Move(float from,float to,float max) => from+Math.Clamp(to-from,-max,max);

    public void Preview(string kind,double now,bool slow=false)
    {
        if (kind is not ("neutral" or "blink" or "happy" or "sad" or "angry" or "relaxed") || !double.IsFinite(now)) return;
        preview=kind;previewStart=now;previewEnd=now+(slow?4:2);ramp=slow?0.7:0.22;
        fromEmotion=Emotion;fromWeight=Weight;fromBlink=Blink;neutralPresented=false;ManualActive=true;
    }
    public void Update(double now,bool automatic,string emotion,float weight,float blink)
    {
        if (!double.IsFinite(now)) return;
        var dt=(float)Math.Clamp(now-lastTime,0,0.1);lastTime=now;
        ManualActive=now<previewEnd;
        if (ManualActive)
        {
            var enter=Ease((now-previewStart)/ramp);
            var exit=1-Ease((now-(previewEnd-ramp))/ramp);
            var target=FaceSelection.Canonical(preview);
            var targetWeight=target=="neutral"?0f:1;
            if(fromEmotion!=target && fromWeight>0)
            {
                // Fade through neutral when changing identity; never jump between two full faces.
                if(enter<0.5f){Emotion=fromEmotion;Weight=fromWeight*(1-2*enter)*exit;}
                else if(!neutralPresented){Emotion="neutral";Weight=0;neutralPresented=true;}
                else {Emotion=target;Weight=targetWeight*(2*enter-1)*exit;}
            }
            else { Emotion=target;Weight=(fromWeight+(targetWeight-fromWeight)*enter)*exit; }
            Blink=(fromBlink+((preview=="blink"?1f:0)-fromBlink)*enter)*exit;
        }
        else
        {
            var target=automatic?FaceSelection.Canonical(emotion):"neutral";
            var desired=target=="neutral"?0:FaceSelection.Intensity(emotion,weight);
            if(Emotion!=target)
            {
                Weight=Move(Weight,0,dt/0.22f);
                if(Weight<=0.001f){Weight=0;Emotion=target;}
            }
            else Weight=Move(Weight,desired,dt/0.22f);
            Blink=automatic && Weight<1f/16 && float.IsFinite(blink)?Math.Clamp(blink,0,1):0;
        }
        var selected=Group;
        var selectedBlink=selected>FaceSelection.FirstGroup && selected<=FaceSelection.FirstGroup+FaceSelection.Steps?(selected-FaceSelection.FirstGroup)/(float)FaceSelection.Steps:0;
        if(selectedBlink>0)
        {
            if(!blinking){blinking=true;peak=0;closedFrames=0;}
            peak=Math.Max(peak,selectedBlink);if(selectedBlink==1)closedFrames++;
        }
        else if(blinking){blinking=false;LastBlinkPeak=peak;LastClosedFrames=closedFrames;CompletedBlinks++;}
    }
    public void Reset(double now)
    {
        preview="";previewEnd=now;lastTime=now;Emotion="neutral";Weight=Blink=0;ManualActive=false;blinking=false;
    }
}
