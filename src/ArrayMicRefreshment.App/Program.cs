using System.Runtime.Versioning;
using ArrayMicRefreshment.App;
using Serilog;

[assembly: SupportedOSPlatform("windows")]

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        path: Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ArrayMicRefreshment",
            "logs",
            "app-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Log.Information("Array Mic Refreshment starting");
    ApplicationConfiguration.Initialize();
    Application.Run(new TrayApplicationContext());
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
