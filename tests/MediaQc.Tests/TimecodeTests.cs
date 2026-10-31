namespace MediaQc.Tests;

public class TimecodeTests
{
    [Theory]
    [InlineData(0, "10:00:00:00")]
    [InlineData(1, "10:00:00:01")]
    [InlineData(25, "10:00:01:00")]
    [InlineData(90000, "11:00:00:00")]
    public void CountsFromTheStartTimecodeAt25(long frame, string expected)
    {
        var timecode = new Timecode(25, 1, "10:00:00:00");

        Assert.Equal(expected, timecode.At(frame / 25.0));
        Assert.False(timecode.DropFrame);
    }

    // SMPTE ST 12-1 drop-frame at 29.97: labels ;00 and ;01 skipped each minute except every tenth.
    [Theory]
    [InlineData(0, "00:00:00;00")]
    [InlineData(1799, "00:00:59;29")]
    [InlineData(1800, "00:01:00;02")]
    [InlineData(17981, "00:09:59;29")]
    [InlineData(17982, "00:10:00;00")]
    [InlineData(107892, "01:00:00;00")]
    public void DropsTwoLabelsAMinuteAt2997(long frame, string expected)
    {
        var timecode = new Timecode(30000, 1001);

        Assert.Equal(expected, timecode.Label(frame));
        Assert.True(timecode.DropFrame);
    }

    [Theory]
    [InlineData(3599, "00:00:59;59")]
    [InlineData(3600, "00:01:00;04")]
    [InlineData(35964, "00:10:00;00")]
    public void DropsFourLabelsAMinuteAt5994(long frame, string expected) =>
        Assert.Equal(expected, new Timecode(60000, 1001).Label(frame));

    [Fact]
    public void StartTimecodeAndLabelAreInverses()
    {
        var zero = new Timecode(30000, 1001);
        foreach (long frame in new long[] { 0, 1, 1799, 1800, 17982, 107891, 2_000_000 })
        {
            string label = zero.Label(frame);
            Assert.Equal(label, new Timecode(30000, 1001, label).At(0));
        }
    }

    [Fact]
    public void NoDropFrameAt23976()
    {
        var timecode = new Timecode(24000, 1001, "01:00:00:00");

        Assert.False(timecode.DropFrame);
        Assert.Equal("01:00:01:00", timecode.Label(86400 + 24));
    }

    [Fact]
    public void WrapsAt24Hours() => Assert.Equal("00:00:00:05", new Timecode(25, 1, "23:59:59:24").At(6 / 25.0));

    [Theory]
    [InlineData("10:00:00")]
    [InlineData("24:00:00:00")]
    [InlineData("10:00:00:25")]
    [InlineData("aa:00:00:00")]
    public void RejectsWhatIsntATimecode(string text) => Assert.Throws<ArgumentException>(() => new Timecode(25, 1, text));

    [Fact]
    public void RejectsADroppedLabel() => Assert.Throws<ArgumentException>(() => new Timecode(30000, 1001, "00:01:00;00"));
}
