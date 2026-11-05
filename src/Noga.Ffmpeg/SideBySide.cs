using System.Runtime.ExceptionServices;

namespace Noga.Ffmpeg;

/// <summary>
/// Runs a check's parts (the picture pass, the shots, the sound) at the same time, each on a thread of its own, and
/// fails as running them one after another in the order given would: when a part fails, the parts after it are
/// stopped, and the first part's failure in that order is the one thrown, so a broken file names the same error as
/// before (docs/decisions/2026-11-04-odd-files.md).
/// </summary>
public static class SideBySide
{
    /// <summary>Runs <paramref name="parts"/> side by side, each given a token that stops it, and waits for them all.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    public static void Run(CancellationToken cancellationToken, params Action<CancellationToken>[] parts)
    {
        ArgumentNullException.ThrowIfNull(parts);
        var stops = parts.Select(_ => CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)).ToArray();
        try
        {
            var tasks = parts.Select((part, i) => Task.Run(() =>
            {
                try
                {
                    part(stops[i].Token);
                }
                catch
                {
                    for (int later = i + 1; later < parts.Length; later++)
                        stops[later].Cancel();
                    throw;
                }
            })).ToArray();
            try
            {
                Task.WaitAll(tasks);
            }
            catch (AggregateException)
            {
            }

            Exception? stopped = null;
            foreach (var task in tasks)
            {
                if (task.Exception?.InnerException is not { } failure)
                    continue;
                if (failure is not OperationCanceledException)
                    ExceptionDispatchInfo.Throw(failure);
                stopped ??= failure;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (stopped is not null)
                ExceptionDispatchInfo.Throw(stopped);
        }
        finally
        {
            foreach (var stop in stops)
                stop.Dispose();
        }
    }
}
