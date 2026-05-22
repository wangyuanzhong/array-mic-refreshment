namespace ArrayMicRefreshment.Integration.Tests;

internal static class StaTestRunner
{
    public static async Task RunAsync(Func<Task> action)
    {
        var tcs = new TaskCompletionSource<object?>();
        var thread = new Thread(() =>
        {
            try
            {
                action().GetAwaiter().GetResult();
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        _ = await tcs.Task.ConfigureAwait(false);
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
        thread.Start();
        thread.Join();
        if (error is not null)
        {
            throw error;
        }

        return result!;
    }
}
