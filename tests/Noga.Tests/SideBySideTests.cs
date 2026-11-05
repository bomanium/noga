using Noga.Ffmpeg;

namespace Noga.Tests;

// A check's parts side by side (docs/decisions/2026-11-05-faster-single-checks.md): the meters on threads of their
// own (Lanes), the picture pass, the shots and the sound at the same time (SideBySide), failing as one after another.
public class SideBySideTests
{
    sealed class Item(int index, int[] disposed) : IDisposable
    {
        public int Index => index;

        public void Dispose() => Interlocked.Increment(ref disposed[index]);
    }

    // Every consumer sees every item, in the order posted, and each item is disposed once, after the last of them.
    [Fact]
    public void EachLaneTakesEveryItemInOrderAndEachIsDisposedOnce()
    {
        const int Count = 200;
        int[] disposed = new int[Count];
        var seen = new[] { new List<int>(), new List<int>(), new List<int>() };
        Action<Item>[] consumers = [.. seen.Select(list => (Action<Item>)(item =>
        {
            Assert.Equal(0, Volatile.Read(ref disposed[item.Index]));
            list.Add(item.Index);
        }))];
        using (var lanes = new Lanes<Item>(consumers, depth: 2))
        {
            for (int i = 0; i < Count; i++)
            {
                if (i % 2 == 0)
                {
                    lanes.Post(new Item(i, disposed));
                }
                else
                {
                    using (lanes.PostHeld(new Item(i, disposed)))
                        Assert.Equal(0, Volatile.Read(ref disposed[i]));
                }
            }

            lanes.Finish();
        }

        Assert.All(seen, list => Assert.Equal(Enumerable.Range(0, Count), list));
        Assert.All(disposed, count => Assert.Equal(1, count));
    }

    // A meter that fails stops the decoding: the decoder's next post, or the finish, throws its exception, and every
    // item is still disposed.
    [Fact]
    public void ALaneThatFailsStopsThePostsWithItsException()
    {
        int[] disposed = new int[1000];
        Action<Item>[] consumers =
        [
            _ => { },
            item =>
            {
                if (item.Index == 10)
                    throw new InvalidDataException("frame 10");
            },
        ];
        var lanes = new Lanes<Item>(consumers);
        var thrown = Assert.Throws<InvalidDataException>(() =>
        {
            for (int i = 0; i < disposed.Length; i++)
                lanes.Post(new Item(i, disposed));
            lanes.Finish();
        });
        lanes.Dispose();

        Assert.Equal("frame 10", thrown.Message);
        int posted = disposed.Count(count => count > 0);
        Assert.InRange(posted, 11, disposed.Length);
        Assert.All(disposed.Take(posted), count => Assert.Equal(1, count));
    }

    // The first part's failure in order is the one thrown, as one after another would: the picture pass's error, not
    // the shots' that came sooner; and the parts after a failed one are stopped.
    [Fact]
    public void TheFirstPartsFailureInOrderIsThrown()
    {
        bool laterStopped = false;
        var thrown = Assert.Throws<InvalidDataException>(() => SideBySide.Run(
            CancellationToken.None,
            _ =>
            {
                Thread.Sleep(200);
                throw new InvalidDataException("the picture pass");
            },
            _ => throw new FormatException("the shots"),
            token =>
            {
                laterStopped = token.WaitHandle.WaitOne(TimeSpan.FromSeconds(30));
                token.ThrowIfCancellationRequested();
            }));

        Assert.Equal("the picture pass", thrown.Message);
        Assert.True(laterStopped);
    }

    [Fact]
    public void PartsRunAtTheSameTime()
    {
        using var both = new Barrier(2);
        SideBySide.Run(
            CancellationToken.None,
            _ => Assert.True(both.SignalAndWait(TimeSpan.FromSeconds(30))),
            _ => Assert.True(both.SignalAndWait(TimeSpan.FromSeconds(30))));
    }

    [Fact]
    public void CancellingStopsEveryPart()
    {
        using var cancel = new CancellationTokenSource();
        cancel.CancelAfter(TimeSpan.FromMilliseconds(100));
        Assert.ThrowsAny<OperationCanceledException>(() => SideBySide.Run(
            cancel.Token,
            token => Task.Delay(TimeSpan.FromSeconds(30), token).Wait(token),
            token => Task.Delay(TimeSpan.FromSeconds(30), token).Wait(token)));
    }

    // Parts of a check's progress that run at the same time add up by their weights; a part of an earlier stage moves
    // nothing.
    [Fact]
    public void PartsOfAStageAddUpByTheirWeights()
    {
        var seen = new List<double>();
        var stages = new CheckProgress(new ReportNow<double>(seen.Add));

        var parts = stages.Together(0.5, 0.3);
        parts[0].Report(0.5);
        parts[1].Report(0.5);
        parts[0].Report(1);
        var next = stages.Together(0.2);
        parts[1].Report(1);
        next[0].Report(1);

        Assert.Equal([0, 0.25, 0.4, 0.65, 0.8, 1], seen.Select(v => Math.Round(v, 6)));
    }
}
