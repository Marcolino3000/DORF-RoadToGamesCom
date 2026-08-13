using System;

namespace Audio
{
/// <summary>
/// Time-stretches audio without moving the pitch, using WSOLA (waveform similarity overlap-add).
///
/// The idea: cut the input into overlapping frames, then lay them back down at a different spacing.
/// Spacing them closer plays the material faster; because every frame keeps its original waveform,
/// the pitch never changes. The catch is that naively re-spaced frames fight each other where they
/// overlap and produce a metallic warble, so before laying each frame down WSOLA slides it a little
/// (within searchRadius) to the position where it best resembles the continuation of the frame
/// already written. That alignment is the whole difference between "usable" and "robot".
///
/// Mono only, and deliberately free of Unity types: the caller reads the samples out of the
/// AudioClip and this runs off the main thread, because a minute of speech costs well over a
/// second to process.
/// </summary>
public static class WsolaTimeStretch
{
    /// <summary>~43 ms at 48 kHz. Long enough to carry a pitch period of a low voice, short enough
    /// that transients do not smear badly.</summary>
    public const int FrameLength = 2048;

    /// <summary>~11 ms of slack for the alignment search, roughly one pitch period of speech.</summary>
    public const int SearchRadius = 512;

    /// <summary>
    /// How much of the overlap the alignment search compares — the whole hop, deliberately. At
    /// 48 kHz a shorter window covers barely one pitch period of a low voice, and the score then
    /// repeats with the lag: candidates a period apart look equally good and the search picks the
    /// wrong one. That octave ambiguity is exactly the warble WSOLA exists to remove, so the
    /// speed-up belongs in <see cref="CoarseStride"/> and not here.
    /// </summary>
    private const int CorrelationLength = 1024;

    /// <summary>
    /// The correlation surface of speech is smooth at this scale, so the search runs on a stride
    /// first and then refines around the winner. Scanning all 1025 offsets one by one is where
    /// almost all the time used to go.
    /// </summary>
    private const int CoarseStride = 8;

    public static float[] Stretch(float[] input, float speed)
    {
        if (input == null || input.Length == 0) return Array.Empty<float>();
        // Outside this range the maths stops being meaningful: analysisHop would round to zero and
        // the loop would rewrite the same frame until it filled a buffer sized from a huge quotient.
        if (speed <= 0.1f || speed >= 4f || Math.Abs(speed - 1f) < 0.001f) return (float[])input.Clone();

        const int frame = FrameLength;
        int synthesisHop = frame / 2;
        int analysisHop = Math.Max(1, (int)Math.Round(synthesisHop * speed));
        // Successive frames must advance, otherwise the alignment search can pick a start behind
        // the previous one and the material stutters or plays backwards.
        int radius = Math.Min(SearchRadius, analysisHop / 2);

        // Too short to overlap-add even twice: a single windowed frame would come out as a bell
        // curve with no partner to restore unity gain, so hand back the original instead.
        if (input.Length <= frame + analysisHop) return (float[])input.Clone();

        var window = HannWindow(frame);
        // Sized from the integers the loop actually steps by, not from the unrounded speed — those
        // disagree whenever the hop rounds down, and the loop would then be cut off mid-way.
        var output = new float[(int)((long)input.Length * synthesisHop / analysisHop) + frame];
        // The stretch of input that "should" come next, used to align the following frame.
        var expected = new float[frame];

        int writePos = 0;
        int frameIndex = 0;

        while (true)
        {
            int naturalStart = frameIndex * analysisHop;
            if (naturalStart + frame >= input.Length) break;
            if (writePos + frame >= output.Length) break;

            int start = frameIndex == 0
                ? naturalStart
                : BestAlignedStart(input, expected, naturalStart, radius, frame);

            for (int i = 0; i < frame; i++)
                output[writePos + i] += input[start + i] * window[i];

            // Remember how the input actually continued past what we just wrote, so the next frame
            // can be slid to match it. Only the part the search reads is worth filling.
            int tailStart = start + synthesisHop;
            for (int i = 0; i < CorrelationLength; i++)
            {
                int idx = tailStart + i;
                expected[i] = idx < input.Length ? input[idx] : 0f;
            }

            writePos += synthesisHop;
            frameIndex++;
        }

        // The loop stops as soon as a whole frame no longer fits, dropping up to one analysis hop
        // of speech — at 2x that is the last 43 ms, usually the final consonant of the last word.
        // One more frame pinned to the very end of the input flushes it.
        int lastStart = input.Length - frame;
        if (lastStart > 0 && writePos + frame <= output.Length)
        {
            for (int i = 0; i < frame; i++)
                output[writePos + i] += input[lastStart + i] * window[i];
            writePos += synthesisHop;
        }

        // Hand back only what was written; the allocation rounds up and the rest is silence.
        int written = Math.Min(writePos + synthesisHop, output.Length);
        if (written < output.Length) Array.Resize(ref output, written);
        return output;
    }

    /// <summary>
    /// Slides the frame within +/- radius of where it would naturally sit and returns the start that
    /// best matches <paramref name="expected"/>. Coarse pass on <see cref="CoarseStride"/>, then one
    /// sample at a time around the winner.
    /// </summary>
    private static int BestAlignedStart(float[] input, float[] expected, int naturalStart, int radius, int frame)
    {
        int from = Math.Max(0, naturalStart - radius);
        int to = Math.Min(input.Length - frame - 1, naturalStart + radius);
        if (to <= from) return Math.Max(0, Math.Min(naturalStart, input.Length - frame - 1));

        int coarse = ScanRange(input, expected, from, to, CoarseStride, naturalStart);
        return ScanRange(input, expected,
            Math.Max(from, coarse - CoarseStride),
            Math.Min(to, coarse + CoarseStride), 1, coarse);
    }

    private static int ScanRange(float[] input, float[] expected, int from, int to, int stride, int preferred)
    {
        // Seeded with the position the frame would take without any sliding. Over silence every
        // candidate scores exactly zero, and a plain > comparison would then hand the win to the
        // leftmost one — dragging every frame after a pause backwards by the full search radius.
        int best = Math.Clamp(preferred, from, to);
        float bestScore = Score(input, expected, best);

        for (int candidate = from; candidate <= to; candidate += stride)
        {
            float score = Score(input, expected, candidate);
            if (score > bestScore)
            {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }

    /// <summary>
    /// Cross-correlation of the candidate against the expected continuation, normalised by the
    /// candidate's own energy so a merely loud stretch cannot outscore a genuinely similar one.
    /// </summary>
    private static float Score(float[] input, float[] expected, int candidate)
    {
        float dot = 0f;
        float energy = 1e-9f;
        for (int i = 0; i < CorrelationLength; i++)
        {
            float s = input[candidate + i];
            dot += s * expected[i];
            energy += s * s;
        }

        return dot / (float)Math.Sqrt(energy);
    }

    /// <summary>Hann, which sums to a constant at 50% overlap — so no renormalising afterwards.</summary>
    private static float[] HannWindow(int length)
    {
        var w = new float[length];
        for (int i = 0; i < length; i++)
            w[i] = 0.5f * (1f - (float)Math.Cos(2.0 * Math.PI * i / (length - 1)));
        return w;
    }
}
}
