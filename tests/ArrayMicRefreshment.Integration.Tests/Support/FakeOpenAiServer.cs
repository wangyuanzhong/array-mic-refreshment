using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace ArrayMicRefreshment.Integration.Tests.Support;

internal sealed class FakeOpenAiServer : IAsyncDisposable
{
    private readonly ConcurrentQueue<string> _assistantContents = new();
    private readonly HttpListener _listener = new();
    private readonly CancellationTokenSource _cts = new();
    private Task? _loop;

    public string ApiBaseUrl { get; }

    public FakeOpenAiServer(params string[] assistantContents)
    {
        foreach (var content in assistantContents)
        {
            _assistantContents.Enqueue(content);
        }

        var port = GetFreePort();
        _listener.Prefixes.Add($"http://127.0.0.1:{port}/");
        ApiBaseUrl = $"http://127.0.0.1:{port}/v1";
    }

    public void Start()
    {
        _listener.Start();
        _loop = Task.Run(ListenLoopAsync);
    }

    private async Task ListenLoopAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            HttpListenerContext ctx;
            try
            {
                ctx = await _listener.GetContextAsync().ConfigureAwait(false);
            }
            catch (HttpListenerException) when (_cts.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }

            _ = Task.Run(() => HandleRequestAsync(ctx), _cts.Token);
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext ctx)
    {
        try
        {
            var path = ctx.Request.Url?.AbsolutePath ?? string.Empty;
            if (!path.Contains("chat/completions", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                ctx.Response.Close();
                return;
            }

            await HandleChatCompletionsAsync(ctx).ConfigureAwait(false);
        }
        catch
        {
            try
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                ctx.Response.Close();
            }
            catch
            {
                // ignored
            }
        }
    }

    private async Task HandleChatCompletionsAsync(HttpListenerContext ctx)
    {
        if (FaultMode == FakeOpenAiFaultMode.Unauthorized)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await WriteBodyAsync(ctx, """{"error":"unauthorized"}""").ConfigureAwait(false);
            return;
        }

        if (FaultMode == FakeOpenAiFaultMode.ServerError)
        {
            ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
            await WriteBodyAsync(ctx, """{"error":"server error"}""").ConfigureAwait(false);
            return;
        }

        if (FaultMode == FakeOpenAiFaultMode.Slow)
        {
            await Task.Delay(TimeSpan.FromSeconds(15), _cts.Token).ConfigureAwait(false);
            ctx.Response.StatusCode = (int)HttpStatusCode.OK;
            await WriteBodyAsync(ctx, ChatCompletionJson("late")).ConfigureAwait(false);
            return;
        }

        if (!_assistantContents.TryDequeue(out var content))
        {
            content = "{}";
        }

        ctx.Response.StatusCode = (int)HttpStatusCode.OK;
        await WriteBodyAsync(ctx, ChatCompletionJson(content)).ConfigureAwait(false);
    }

    private static async Task WriteBodyAsync(HttpListenerContext ctx, string json)
    {
        var bytes = Encoding.UTF8.GetBytes(json);
        ctx.Response.ContentType = "application/json";
        ctx.Response.ContentLength64 = bytes.Length;
        await ctx.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        ctx.Response.Close();
    }

    private static string ChatCompletionJson(string assistantContent)
    {
        var escaped = JsonSerializer.Serialize(assistantContent);
        return "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":" + escaped + "}}]}";
    }

    public FakeOpenAiFaultMode FaultMode { get; set; } = FakeOpenAiFaultMode.None;

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync().ConfigureAwait(false);
        _listener.Stop();
        _listener.Close();
        if (_loop is not null)
        {
            try
            {
                await _loop.ConfigureAwait(false);
            }
            catch
            {
                // ignored
            }
        }

        _cts.Dispose();
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}

public enum FakeOpenAiFaultMode
{
    None,
    Unauthorized,
    ServerError,
    Slow,
}
