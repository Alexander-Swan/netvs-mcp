using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace NetVsMcp.Vsix;

/// <summary>
/// Minimal Chrome DevTools Protocol (CDP) client: WebSocket framing plus JSON-RPC-style
/// command/response matching against a single browser page target. Extracted from the former
/// monolithic AutomationCapabilityService (see ARCH-7 in docs/IMPROVEMENT_PLAN.md) so it can be
/// unit-tested and reasoned about independently of the web-debug capability service that owns it.
/// </summary>
internal sealed class CdpClient : IDisposable
{
    private readonly ClientWebSocket socket;
    private readonly SemaphoreSlim commandLock = new(1, 1);
    private readonly List<string> consoleEntries = new();
    private readonly List<string> networkEntries = new();
    private int nextId;

    private CdpClient(Uri endpoint, Uri webSocketUri, string? targetUrl, ClientWebSocket socket)
    {
        Endpoint = endpoint;
        WebSocketUri = webSocketUri;
        TargetUrl = targetUrl;
        this.socket = socket;
    }

    public Uri Endpoint { get; }
    public Uri WebSocketUri { get; }
    public string? TargetUrl { get; private set; }

    public static async Task<CdpClient> ConnectAsync(Uri endpoint, string? requestedUrl, CancellationToken cancellationToken)
    {
        var target = await ResolveTargetAsync(endpoint, requestedUrl, cancellationToken);
        var websocket = new ClientWebSocket();
        await websocket.ConnectAsync(target.WebSocketUri, cancellationToken);
        try
        {
            var client = new CdpClient(endpoint, target.WebSocketUri, target.Url, websocket);
            await client.SendCommandAsync("Runtime.enable", null, cancellationToken);
            await client.SendCommandAsync("Network.enable", null, cancellationToken);
            await client.SendCommandAsync("Page.enable", null, cancellationToken);
            return client;
        }
        catch
        {
            // If any of the enable calls above fails, the websocket connected at the top of
            // this method would otherwise be orphaned: it's only reachable through the
            // never-returned CdpClient instance, so nothing would ever dispose it. Dispose it
            // here before rethrowing so a slow/hiccuping browser during web_connect doesn't
            // leak a live WebSocket for the life of the VS process.
            websocket.Dispose();
            throw;
        }
    }

    public async Task NavigateAsync(string url, CancellationToken cancellationToken)
    {
        await SendCommandAsync("Page.navigate", "{\"url\":" + JsonSerializer.Serialize(url) + "}", cancellationToken);
        TargetUrl = url;
    }

    public async Task<string> CaptureScreenshotAsync(CancellationToken cancellationToken)
    {
        using var document = await SendCommandAsync("Page.captureScreenshot", "{\"format\":\"png\",\"fromSurface\":true}", cancellationToken);
        return document.RootElement.GetProperty("result").GetProperty("data").GetString() ?? string.Empty;
    }

    public async Task<string> EvaluateStringAsync(string expression, CancellationToken cancellationToken)
    {
        var result = await EvaluateAsync(expression, cancellationToken);
        return result;
    }

    public async Task<string> EvaluateAsync(string expression, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return string.Empty;
        }

        var parameters = "{\"expression\":" + JsonSerializer.Serialize(expression) + ",\"returnByValue\":true,\"awaitPromise\":true}";
        using var document = await SendCommandAsync("Runtime.evaluate", parameters, cancellationToken);
        var root = document.RootElement;
        if (root.TryGetProperty("result", out var commandResult) &&
            commandResult.TryGetProperty("exceptionDetails", out var exception))
        {
            return JsonSerializer.Serialize(exception);
        }

        if (!root.TryGetProperty("result", out commandResult) ||
            !commandResult.TryGetProperty("result", out var evaluation))
        {
            return string.Empty;
        }

        if (evaluation.TryGetProperty("value", out var value))
        {
            return value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.GetRawText();
        }

        if (evaluation.TryGetProperty("description", out var description))
        {
            return description.GetString() ?? string.Empty;
        }

        return evaluation.GetRawText();
    }

    public async Task FlushEventsAsync(CancellationToken cancellationToken)
    {
        await EvaluateAsync("undefined", cancellationToken);
    }

    public IReadOnlyCollection<string> GetConsoleEntries() => consoleEntries.ToArray();

    public IReadOnlyCollection<string> GetNetworkEntries() => networkEntries.ToArray();

    public void Dispose()
    {
        commandLock.Dispose();
        socket.Dispose();
    }

    private async Task<JsonDocument> SendCommandAsync(string method, string? parametersJson, CancellationToken cancellationToken)
    {
        await commandLock.WaitAsync(cancellationToken);
        try
        {
            var id = Interlocked.Increment(ref nextId);
            var payload = parametersJson is null
                ? "{\"id\":" + id + ",\"method\":" + JsonSerializer.Serialize(method) + "}"
                : "{\"id\":" + id + ",\"method\":" + JsonSerializer.Serialize(method) + ",\"params\":" + parametersJson + "}";
            var bytes = Encoding.UTF8.GetBytes(payload);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, cancellationToken);

            while (true)
            {
                var document = await ReceiveDocumentAsync(cancellationToken);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var responseId) && responseId.GetInt32() == id)
                {
                    return document;
                }

                ProcessEvent(root);
                document.Dispose();
            }
        }
        finally
        {
            commandLock.Release();
        }
    }

    private async Task<JsonDocument> ReceiveDocumentAsync(CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[8192];
        WebSocketReceiveResult result;
        do
        {
            result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException("The browser debug protocol connection closed.");
            }

            await stream.WriteAsync(buffer, 0, result.Count, cancellationToken);
        }
        while (!result.EndOfMessage);

        return JsonDocument.Parse(stream.ToArray());
    }

    private void ProcessEvent(JsonElement root)
    {
        if (!root.TryGetProperty("method", out var methodProperty))
        {
            return;
        }

        var method = methodProperty.GetString() ?? string.Empty;
        if (method.Equals("Runtime.consoleAPICalled", StringComparison.OrdinalIgnoreCase))
        {
            consoleEntries.Add(FormatConsoleEvent(root));
        }
        else if (method.Equals("Runtime.exceptionThrown", StringComparison.OrdinalIgnoreCase))
        {
            consoleEntries.Add(FormatExceptionEvent(root));
        }
        else if (method.Equals("Network.requestWillBeSent", StringComparison.OrdinalIgnoreCase) ||
                 method.Equals("Network.responseReceived", StringComparison.OrdinalIgnoreCase))
        {
            networkEntries.Add(FormatNetworkEvent(root));
        }
    }

    private static async Task<CdpTarget> ResolveTargetAsync(Uri endpoint, string? requestedUrl, CancellationToken cancellationToken)
    {
        using var client = new WebClient { Encoding = Encoding.UTF8 };
        using var registration = cancellationToken.Register(client.CancelAsync);
        var listUri = new Uri(endpoint, "/json/list");
        var text = await client.DownloadStringTaskAsync(listUri);
        using var document = JsonDocument.Parse(text);
        var targets = document.RootElement.EnumerateArray()
            .Select(element => new CdpTarget(
                GetJsonString(element, "url"),
                GetJsonString(element, "title"),
                GetJsonString(element, "type"),
                new Uri(GetJsonString(element, "webSocketDebuggerUrl") ?? throw new InvalidOperationException("CDP target is missing webSocketDebuggerUrl."))))
            .ToArray();

        var selected = SelectTarget(targets, requestedUrl);
        if (selected is null)
        {
            throw new InvalidOperationException("No page target was available from the browser debug protocol endpoint.");
        }

        return selected;
    }

    internal static CdpTarget? SelectTarget(IReadOnlyCollection<CdpTarget> targets, string? requestedUrl)
    {
        if (!string.IsNullOrWhiteSpace(requestedUrl))
        {
            var expected = requestedUrl!;
            var matching = targets.FirstOrDefault(target =>
                ContainsOrdinalIgnoreCase(target.Url, expected) ||
                ContainsOrdinalIgnoreCase(target.Title, expected));
            if (matching is not null)
            {
                return matching;
            }
        }

        return targets.FirstOrDefault(target => string.Equals(target.Type, "page", StringComparison.OrdinalIgnoreCase))
            ?? targets.FirstOrDefault();
    }

    private static string FormatConsoleEvent(JsonElement root)
    {
        var parameters = root.GetProperty("params");
        var kind = GetJsonString(parameters, "type") ?? "log";
        var args = parameters.TryGetProperty("args", out var argsElement)
            ? argsElement.EnumerateArray().Select(FormatRemoteObject)
            : Enumerable.Empty<string>();
        return $"{kind}: {string.Join(" ", args)}";
    }

    private static string FormatExceptionEvent(JsonElement root)
    {
        var parameters = root.GetProperty("params");
        if (parameters.TryGetProperty("exceptionDetails", out var details))
        {
            return "exception: " + (GetJsonString(details, "text") ?? details.GetRawText());
        }

        return "exception";
    }

    private static string FormatNetworkEvent(JsonElement root)
    {
        var method = GetJsonString(root, "method") ?? "Network";
        var parameters = root.GetProperty("params");
        if (parameters.TryGetProperty("request", out var request))
        {
            return $"{method}: {GetJsonString(request, "method")} {GetJsonString(request, "url")}";
        }

        if (parameters.TryGetProperty("response", out var response))
        {
            return $"{method}: {GetJsonString(response, "status")} {GetJsonString(response, "url")}";
        }

        return method;
    }

    private static string FormatRemoteObject(JsonElement element)
    {
        if (element.TryGetProperty("value", out var value))
        {
            return value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.GetRawText();
        }

        return GetJsonString(element, "description") ?? element.GetRawText();
    }

    private static bool ContainsOrdinalIgnoreCase(string? value, string expected) =>
        value?.IndexOf(expected, StringComparison.OrdinalIgnoreCase) >= 0;

    private static string? GetJsonString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) ? property.ToString() : null;
}

internal sealed class CdpTarget
{
    public CdpTarget(string? url, string? title, string? type, Uri webSocketUri)
    {
        Url = url;
        Title = title;
        Type = type;
        WebSocketUri = webSocketUri;
    }

    public string? Url { get; }
    public string? Title { get; }
    public string? Type { get; }
    public Uri WebSocketUri { get; }
}
