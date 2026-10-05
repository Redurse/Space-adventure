using Anabiosis.Client.Rendering;

internal static partial class TestRunner
{
    private static bool StudioSplash_Progress_StartsAtZeroEndsAtOneAndNeverGoesBackwards()
    {
        Func<float, float>[] curves =
        {
            StudioSplash.RingProgress, StudioSplash.GlyphProgress,
            t => StudioSplash.LetterAlpha(t, 0), t => StudioSplash.LetterAlpha(t, StudioSplash.Name.Length - 1),
            t => StudioSplash.SubtitleAlpha(t, 0), t => StudioSplash.SubtitleAlpha(t, StudioSplash.Subtitle.Length - 1),
        };
        foreach (var curve in curves)
        {
            if (curve(0f) != 0f || curve(StudioSplash.Duration) != 1f)
                return false;
            var previous = 0f;
            for (var t = 0f; t <= StudioSplash.Duration; t += 0.01f)
            {
                var v = curve(t);
                if (v < previous - 1e-5f || v < 0f || v > 1f)
                    return false;
                previous = v;
            }
        }
        return true;
    }

    private static bool StudioSplash_LettersArriveInOrderAndEverythingIsInBeforeTheFade()
    {
        var firstArrival = Enumerable.Range(0, StudioSplash.Name.Length)
            .Select(i => { var t = 0f; while (StudioSplash.LetterAlpha(t, i) <= 0f) t += 0.005f; return t; }).ToList();
        var ordered = firstArrival.Zip(firstArrival.Skip(1), (a, b) => b > a).All(x => x);
        var fadeStart = StudioSplash.Duration - StudioSplash.FadeOutSeconds;
        return ordered
            && StudioSplash.LetterAlpha(fadeStart, StudioSplash.Name.Length - 1) == 1f
            && StudioSplash.SubtitleAlpha(fadeStart, StudioSplash.Subtitle.Length - 1) == 1f;
    }

    private static bool StudioSplash_VisibilityFadesOutAtTheEndAndTheSceneIsDoneAfterItsDuration()
    {
        return StudioSplash.Visibility(0f) == 1f
            && StudioSplash.Visibility(StudioSplash.Duration - StudioSplash.FadeOutSeconds) == 1f
            && StudioSplash.Visibility(StudioSplash.Duration) == 0f
            && !StudioSplash.IsDone(StudioSplash.Duration - 0.01f)
            && StudioSplash.IsDone(StudioSplash.Duration)
            && StudioSplash.SkippableAfter < StudioSplash.Duration;
    }
}
