namespace MediaQc.Tests;

public class LineUpTests
{
    static readonly ToneSpan Tone = new(0, 5, 1000, -18);

    [Fact]
    public void ToneThatStartsTheFileIsTheLineUp() => Assert.Equal(5, LineUp.End([Tone], []));

    [Fact]
    public void ToneAfterOnlySilenceIsTheLineUp() =>
        Assert.Equal(12, LineUp.End([Tone with { Start = 4, End = 12 }], [new TimeRange(0, 3.99)]));

    [Fact]
    public void ToneAfterSoundIsntTheLineUp() =>
        Assert.Equal(0, LineUp.End([Tone with { Start = 4, End = 12 }], [new TimeRange(0, 2)]));

    [Fact]
    public void WithoutToneLoudnessStartsAtTheTop() => Assert.Equal(0, LineUp.End([], [new TimeRange(0, 3)]));
}
