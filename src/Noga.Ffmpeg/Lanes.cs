using System.Collections.Concurrent;
using System.Runtime.ExceptionServices;

namespace Noga.Ffmpeg;

/// <summary>
/// Hands each item (a decoded frame) to several consumers (the meters), each on a thread of its own and each taking
/// the items in the order posted, so they measure side by side and while the next frame is decoded, rather than one
/// after another (docs/notes/performance.md). An item is disposed once every consumer is done with it. At most
/// <c>depth</c> items wait for a consumer, so a slow one holds the decoder back rather than filling the memory.
/// The first consumer to fail stops the rest: <see cref="Post"/> and <see cref="Finish"/> throw its exception.
/// </summary>
public sealed class Lanes<T> : IDisposable
    where T : class, IDisposable
{
    readonly IReadOnlyList<Action<T>> _consumers;
    readonly BlockingCollection<Shared>[] _queues;
    readonly Task[] _workers;
    readonly CancellationTokenSource _stop;
    readonly CancellationToken _cancellation;
    ExceptionDispatchInfo? _failure;
    bool _finished;

    /// <summary>Starts a thread for each of <paramref name="consumers"/>, each with at most <paramref name="depth"/> items waiting.</summary>
    public Lanes(IReadOnlyList<Action<T>> consumers, int depth = 4, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(consumers);
        ArgumentOutOfRangeException.ThrowIfLessThan(depth, 1);
        _consumers = consumers;
        _cancellation = cancellationToken;
        _stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _queues = [.. consumers.Select(_ => new BlockingCollection<Shared>(depth))];
        _workers = [.. Enumerable.Range(0, consumers.Count).Select(i => Task.Factory.StartNew(() => Run(i), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default))];
    }

    /// <summary>
    /// Gives <paramref name="item"/> to every consumer, waiting while one has <c>depth</c> waiting; the lanes own it
    /// from here (and dispose it, also when this throws).
    /// </summary>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public void Post(T item) => Give(item, _queues.Length);

    /// <summary>
    /// As <see cref="Post"/>, the item kept until the handle returned is disposed too, for a caller that still reads it
    /// (ShotDetector, which reads a frame until it pulls the next).
    /// </summary>
    /// <exception cref="OperationCanceledException">Cancelled.</exception>
    public IDisposable PostHeld(T item) => Give(item, _queues.Length + 1);

    Shared Give(T item, int users)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_stop.IsCancellationRequested)
        {
            item.Dispose();
            ThrowIfFailed();
            _stop.Token.ThrowIfCancellationRequested();
        }

        var shared = new Shared(item, users);
        int given = 0;
        try
        {
            for (; given < _queues.Length; given++)
                _queues[given].Add(shared, _stop.Token);
        }
        catch (OperationCanceledException)
        {
            for (; given < users; given++)
                shared.Release();
            ThrowIfFailed();
            throw;
        }

        return shared;
    }

    /// <summary>Waits for every consumer to finish what was posted; throws the first one's failure.</summary>
    public void Finish()
    {
        _finished = true;
        foreach (var queue in _queues)
            queue.CompleteAdding();
        Task.WaitAll(_workers);
        ThrowIfFailed();
        _cancellation.ThrowIfCancellationRequested();
    }

    /// <summary>Stops the consumers (unless finished), releases what is still waiting and waits for the threads.</summary>
    public void Dispose()
    {
        if (!_finished)
        {
            _stop.Cancel();
            foreach (var queue in _queues)
                queue.CompleteAdding();
            try
            {
                Task.WaitAll(_workers);
            }
            catch (AggregateException)
            {
            }
        }

        foreach (var queue in _queues)
            queue.Dispose();
        _stop.Dispose();
    }

    void ThrowIfFailed() => Volatile.Read(ref _failure)?.Throw();

    // Takes every item posted to this lane, measuring it unless the lanes are stopping, and releases it either way.
    void Run(int lane)
    {
        foreach (var shared in _queues[lane].GetConsumingEnumerable())
        {
            try
            {
                if (!_stop.IsCancellationRequested)
                    _consumers[lane](shared.Item);
            }
            catch (Exception e)
            {
                Interlocked.CompareExchange(ref _failure, ExceptionDispatchInfo.Capture(e), null);
                _stop.Cancel();
            }
            finally
            {
                shared.Release();
            }
        }
    }

    sealed class Shared(T item, int users) : IDisposable
    {
        int _users = users, _held = 1;

        public T Item => item;

        // The holder's release (PostHeld's caller), once.
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _held, 0) == 1)
                Release();
        }

        public void Release()
        {
            if (Interlocked.Decrement(ref _users) == 0)
                item.Dispose();
        }
    }
}
