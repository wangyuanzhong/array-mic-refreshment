using System.Collections.Concurrent;

namespace ArrayMicRefreshment.Integration.Tests;

/// <summary>
/// Runs async work on a dedicated STA thread with a single-threaded synchronization
/// context so that <c>await</c> continuations resume on the same STA thread.
/// Required for <see cref="System.Windows.Forms.Clipboard"/> and other OLE-bound APIs.
/// </summary>
internal static class StaTestRunner
{
    public static Task RunAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource<object?>();
        var thread = new Thread(() =>
        {
            var prev = SynchronizationContext.Current;
            var ctx = new SingleThreadSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(ctx);
            try
            {
                var task = action();
                task.ContinueWith(
                    _ => ctx.Complete(),
                    TaskScheduler.Default);
                ctx.RunOnCurrentThread();
                task.GetAwaiter().GetResult();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.TrySetException(ex);
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(prev);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        return tcs.Task;
    }

    public static T Run<T>(Func<T> action)
    {
        T? result = default;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try
            {
                result = action();
            }
            catch (Exception ex)
            {
                error = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            throw error;
        }

        return result!;
    }

    private sealed class SingleThreadSynchronizationContext : SynchronizationContext
    {
        private readonly BlockingCollection<(SendOrPostCallback, object?)> _queue = new();

        public override void Post(SendOrPostCallback d, object? state) => _queue.Add((d, state));

        public override void Send(SendOrPostCallback d, object? state) =>
            throw new NotSupportedException("Synchronous Send is not supported.");

        public void RunOnCurrentThread()
        {
            foreach (var (callback, state) in _queue.GetConsumingEnumerable())
            {
                callback(state);
            }
        }

        public void Complete() => _queue.CompleteAdding();
    }
}
