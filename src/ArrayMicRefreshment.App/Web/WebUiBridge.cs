using System.Runtime.InteropServices;
using System.Text.Json;

namespace ArrayMicRefreshment.App.Web;

/// <summary>
/// COM-visible bridge exposed to WebView2 as <c>window.chrome.webview.hostObjects.amr</c>.
/// </summary>
[ClassInterface(ClassInterfaceType.AutoDual)]
[ComVisible(true)]
public sealed partial class WebUiBridge : IWebUiBridge
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly WebUiBridgeContext _context;

    public WebUiBridge(WebUiBridgeContext context)
    {
        _context = context;
    }

    public string GetAppInfo()
    {
        return JsonSerializer.Serialize(new
        {
            version = AppInfo.Version,
            platform = "win-x64",
        }, JsonOptions);
    }

    public void RequestClose(bool success)
    {
        RunOnUi(() =>
        {
            var form = _context.HostForm;
            if (form is null || form.IsDisposed)
            {
                return;
            }

            if (success)
            {
                _context.OnSuccess?.Invoke();
            }

            if (form.HideOnClose)
            {
                form.Hide();
                return;
            }

            form.DialogResult = success ? DialogResult.OK : DialogResult.Cancel;
            form.Close();
        });
    }

    private void RunOnUi(Action action)
    {
        var form = _context.HostForm;
        if (form is null || form.IsDisposed)
        {
            action();
            return;
        }

        if (form.InvokeRequired)
        {
            form.Invoke(action);
        }
        else
        {
            action();
        }
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
}
