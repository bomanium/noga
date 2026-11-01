namespace MediaQc.Tests;

public class SilenceFinderTests
{
    const int Rate = 1000;

    // Stereo at 1 kHz: 1 s of sound, 3 s of silence, 1 s of sound, 2.5 s of silence to the end.
    static float[] Programme()
    {
        var samples = new float[7500 * 2];
        for (int i = 0; i < 7500; i++)
        {
            bool sound = i < 1000 || (i >= 4000 && i < 5000);
            samples[2 * i] = samples[(2 * i) + 1] = sound ? 0.5f : 0.0005f;
        }

        return samples;
    }

    [Fact]
    public void SilencesStartAtTheirFirstSilentInstantAndEndAtTheFirstSound()
    {
        var finder = new SilenceFinder(Rate, 2, -60, 2);
        finder.Add(Programme(), 0);

        Assert.Equal([new TimeRange(1, 4), new TimeRange(5, 7.5)], finder.Finish());
    }

    [Fact]
    public void ChunksGiveTheSameAsOneCall()
    {
        var whole = new SilenceFinder(Rate, 2, -60, 2);
        whole.Add(Programme(), 0);
        var chunked = new SilenceFinder(Rate, 2, -60, 2);
        float[] samples = Programme();
        for (int start = 0; start < samples.Length; start += 2 * 333)
            chunked.Add(samples.AsSpan(start, Math.Min(2 * 333, samples.Length - start)), start / 2);

        Assert.Equal(whole.Finish(), chunked.Finish());
    }

    [Fact]
    public void ShorterThanTheMinimumIsntASilence()
    {
        var finder = new SilenceFinder(Rate, 2, -60, 3.5);
        finder.Add(Programme(), 0);

        Assert.Empty(finder.Finish());
    }

    [Fact]
    public void OneLoudChannelIsntSilence()
    {
        float[] samples = Programme();
        for (int i = 1000; i < 4000; i++)
            samples[2 * i] = 0.5f;
        var finder = new SilenceFinder(Rate, 2, -60, 2);
        finder.Add(samples, 0);

        Assert.Equal([new TimeRange(5, 7.5)], finder.Finish());
    }
}
