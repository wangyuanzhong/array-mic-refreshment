using ArrayMicRefreshment.App;
using Serilog;

namespace ArrayMicRefreshment.Integration.Tests;

public sealed class TrayApplicationContextSmokeTests
{
    [Fact]
    [Trait("Category", "TraySmoke")]
    public async Task TrayApplicationContext_runs_and_exits_without_exception()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        Exception? fault = null;
        var done = new ManualResetEventSlim(false);

        var thread = new Thread(() =>
        {
            try
            {
                Log.Logger = new LoggerConfiguration()
                    .MinimumLevel.Fatal()
                    .CreateLogger();
                ApplicationConfiguration.Initialize();
                using var exitTimer = new System.Windows.Forms.Timer { Interval = 5000 };
                exitTimer.Tick += (_, _) =>
                {
                    exitTimer.Stop();
                    Application.Exit();
                };
                exitTimer.Start();
                Application.Run(new TrayApplicationContext());
            }
            catch (Exception ex)
            {
                fault = ex;
            }
            finally
            {
                Log.CloseAndFlush();
                done.Set();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        Assert.True(done.Wait(TimeSpan.FromSeconds(20)), "Tray smoke test timed out waiting for UI thread exit.");
        Assert.Null(fault);
        await Task.CompletedTask.ConfigureAwait(false);
    }
}
