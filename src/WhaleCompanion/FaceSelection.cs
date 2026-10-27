namespace WhaleCompanion;

// build_face_runtime_v3.py: neutral + eight samples per authored shape.
public static class FaceSelection
{
    public const int FirstGroup = 56000;
    public const int Steps = 8;
    public const int StateCount = 41;
    public static string Canonical(string emotion) => emotion == "concerned" ? "sad" : emotion is "happy" or "sad" or "angry" or "relaxed" ? emotion : "neutral";
    public static float Intensity(string emotion, float weight) => !float.IsFinite(weight) ? 0 : Math.Clamp(weight,0,1) * (emotion == "concerned" ? 0.6f : 1);
    private static int Step(float weight) => !float.IsFinite(weight) ? 0 : (int)MathF.Round(Math.Clamp(weight,0,1) * Steps, MidpointRounding.AwayFromZero);
    public static int Select(string emotion, float weight, float blink)
    {
        var canonical = Canonical(emotion);
        int offset = canonical switch { "happy" => 8, "sad" => 16, "angry" => 24, "relaxed" => 32, _ => 0 };
        var step = offset > 0 ? Step(Intensity(emotion, weight)) : 0;
        // Authored relaxed card motion passes through the face in partial samples.
        // Only neutral and the verified terminal are safe until those samples are rebuilt.
        if (canonical == "relaxed" && step > 0) step = Steps;
        // Avoid replacing the authored mouth/eyes with a neutral blink during emotion playback.
        return FirstGroup + (step > 0 ? offset + step : Step(blink));
    }
    public static bool IsIntermediate(int group) => group > FirstGroup && group < FirstGroup + StateCount && (group - FirstGroup) % Steps != 0;
    public static bool TryEvaluate(int group,int selected,out bool visible)
    {
        visible = group == selected;
        return group >= FirstGroup && group < FirstGroup + StateCount;
    }
}
