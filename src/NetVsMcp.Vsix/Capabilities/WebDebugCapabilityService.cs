using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using DiagnosticsProcess = System.Diagnostics.Process;

namespace NetVsMcp.Vsix;

/// <summary>
/// Browser web-debugging automation, backed by <see cref="CdpClient"/> (Chrome DevTools
/// Protocol) when a debug endpoint is reachable, falling back to shell-launch + desktop UIA
/// (via <see cref="UiAutomationCapabilityService"/>) otherwise. Extracted from the former
/// monolithic AutomationCapabilityService.
/// </summary>
internal sealed class WebDebugCapabilityService
{
    private readonly UiAutomationCapabilityService ui;
    private readonly object stateLock = new();
    private CdpClient? cdp;
    private string? connectedWebTarget;
    private string? connectedWebUrl;

    public WebDebugCapabilityService(UiAutomationCapabilityService ui)
    {
        this.ui = ui;
    }

    public async Task<AutomationResult> WebConnectAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DisposeCdp();
        var endpoint = ResolveCdpEndpoint(request.Target);
        if (endpoint is not null)
        {
            try
            {
                var client = await CdpClient.ConnectAsync(endpoint, request.Url, cancellationToken);
                string? url;
                lock (stateLock)
                {
                    cdp = client;
                    connectedWebTarget = endpoint.ToString();
                    connectedWebUrl = client.TargetUrl;
                    url = connectedWebUrl;
                }

                return AutomationSupport.Success(request, null, ("backend", "cdp"), ("endpoint", endpoint.ToString()), ("url", url ?? string.Empty));
            }
            catch (Exception ex) when (ex is WebException or WebSocketException or JsonException or InvalidOperationException)
            {
                string? url;
                lock (stateLock)
                {
                    connectedWebTarget = request.Target;
                    connectedWebUrl = request.Url ?? connectedWebUrl;
                    url = connectedWebUrl;
                }

                return AutomationSupport.Success(request, null, ("backend", "browser-shell-uia"), ("cdpMessage", ex.Message), ("url", url ?? string.Empty));
            }
        }

        string? finalUrl;
        lock (stateLock)
        {
            connectedWebTarget = request.Target;
            connectedWebUrl = request.Url ?? connectedWebUrl;
            finalUrl = connectedWebUrl;
        }

        if (!string.IsNullOrWhiteSpace(request.Url))
        {
            DiagnosticsProcess.Start(request.Url);
        }

        return AutomationSupport.Success(request, null, ("backend", "browser-shell-uia"), ("url", finalUrl ?? string.Empty));
    }

    public Task<AutomationResult> WebDisconnectAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DisposeCdp();
        lock (stateLock)
        {
            connectedWebTarget = null;
            connectedWebUrl = null;
        }

        return Task.FromResult(AutomationSupport.Success(request, null, ("backend", "cdp")));
    }

    public Task<AutomationResult> WebStatusAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        CdpClient? cdpClient;
        string? target;
        string? url;
        lock (stateLock)
        {
            cdpClient = cdp;
            target = connectedWebTarget;
            url = connectedWebUrl;
        }

        var text = cdpClient is not null
            ? $"connected=true; backend=cdp; target={target}; url={url}; websocket={cdpClient.WebSocketUri}"
            : $"connected={target is not null || url is not null}; backend=browser-shell-uia; target={target}; url={url}";
        return Task.FromResult(AutomationSupport.Success(request, text, ("backend", cdpClient is null ? "browser-shell-uia" : "cdp")));
    }

    public async Task<AutomationResult> WebNavigateAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(request.Url))
        {
            return AutomationSupport.Failure(request, "URL is required for browser navigation.", ("backend", "browser-shell"));
        }

        lock (stateLock)
        {
            connectedWebUrl = request.Url;
        }

        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            await client.NavigateAsync(request.Url!, cancellationToken);
            return AutomationSupport.Success(request, null, ("backend", "cdp"), ("url", request.Url ?? string.Empty));
        });
        if (cdpResult is not null)
        {
            return cdpResult;
        }

        DiagnosticsProcess.Start(request.Url);
        return AutomationSupport.Success(request, null, ("backend", "browser-shell"), ("url", request.Url ?? string.Empty));
    }

    public async Task<AutomationResult> WebScreenshotAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var image = await client.CaptureScreenshotAsync(cancellationToken);
            return AutomationSupport.Success(request, image, ("backend", "cdp"), ("encoding", "base64"), ("format", "png"));
        });
        if (cdpResult is not null)
        {
            return cdpResult;
        }

        return await ui.UiCaptureWindowAsync(WithWebTarget(request), cancellationToken);
    }

    public async Task<AutomationResult> WebDomGetAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var liveHtml = await client.EvaluateStringAsync("document.documentElement ? document.documentElement.outerHTML : ''", cancellationToken);
            return AutomationSupport.Success(request, AutomationSupport.Truncate(liveHtml), ("backend", "cdp"), ("url", GetConnectedWebUrl() ?? string.Empty));
        });
        if (cdpResult is not null)
        {
            return cdpResult;
        }

        var url = request.Url ?? GetConnectedWebUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            return AutomationSupport.Failure(request, "A URL is required before DOM fetch.", ("backend", "http-fetch"));
        }

        var resolvedUrl = url ?? string.Empty;
        var html = DownloadText(resolvedUrl);
        return AutomationSupport.Success(request, AutomationSupport.Truncate(html), ("backend", "http-fetch"), ("url", resolvedUrl));
    }

    public async Task<AutomationResult> WebDomQueryAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var selectorJson = JsonSerializer.Serialize(request.Selector ?? string.Empty);
            var expression = $"Array.from(document.querySelectorAll({selectorJson})).map(e => e.outerHTML).join('\\n')";
            var result = await client.EvaluateStringAsync(expression, cancellationToken);
            var count = string.IsNullOrEmpty(result) ? 0 : result.Split('\n').Length;
            return AutomationSupport.Success(request, AutomationSupport.Truncate(result), ("backend", "cdp"), ("matchCount", count.ToString()));
        });
        if (cdpResult is not null)
        {
            return cdpResult;
        }

        var url = request.Url ?? GetConnectedWebUrl();
        if (string.IsNullOrWhiteSpace(url))
        {
            return AutomationSupport.Failure(request, "A URL is required before DOM query.", ("backend", "http-fetch"));
        }

        var resolvedUrl = url ?? string.Empty;
        var html = DownloadText(resolvedUrl);
        var matches = QueryHtml(html, request.Selector).ToArray();
        return AutomationSupport.Success(request, string.Join(Environment.NewLine, matches), ("backend", "http-fetch"), ("matchCount", matches.Length.ToString()));
    }

    public async Task<AutomationResult> WebConsoleAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            await client.FlushEventsAsync(cancellationToken);
            var entries = client.GetConsoleEntries();
            return AutomationSupport.Success(request, string.Join(Environment.NewLine, entries), ("backend", "cdp"), ("entryCount", entries.Count.ToString()));
        });
        if (cdpResult is not null)
        {
            return cdpResult;
        }

        return AutomationSupport.Success(request, string.Empty, ("backend", "browser-shell-uia"), ("message", "Browser console capture requires CDP; no console entries are available from the shell backend."));
    }

    public async Task<AutomationResult> WebJsExecuteAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var result = await client.EvaluateAsync(request.Text ?? string.Empty, cancellationToken);
            return AutomationSupport.Success(request, result, ("backend", "cdp"));
        });

        return cdpResult
            ?? AutomationSupport.Failure(request, "JavaScript execution requires a connected browser debug protocol backend; call web_connect with a CDP endpoint first.", ("backend", "browser-shell-uia"));
    }

    public async Task<AutomationResult> WebNetworkAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            await client.FlushEventsAsync(cancellationToken);
            var entries = client.GetNetworkEntries();
            return AutomationSupport.Success(request, string.Join(Environment.NewLine, entries), ("backend", "cdp"), ("entryCount", entries.Count.ToString()));
        });
        if (cdpResult is not null)
        {
            return cdpResult;
        }

        return AutomationSupport.Success(request, string.Empty, ("backend", "browser-shell-uia"), ("message", "Network capture requires CDP; no network events are available from the shell backend."));
    }

    public async Task<AutomationResult> WebElementClickAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var selectorJson = JsonSerializer.Serialize(request.Selector ?? string.Empty);
            var result = await client.EvaluateAsync($"(() => {{ const e = document.querySelector({selectorJson}); if (!e) return false; e.click(); return true; }})()", cancellationToken);
            return AutomationSupport.Success(request, result, ("backend", "cdp"));
        });
        if (cdpResult is not null)
        {
            return cdpResult;
        }

        return await ui.UiClickAsync(WithWebTarget(request), cancellationToken);
    }

    public async Task<AutomationResult> WebElementSetValueAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var selectorJson = JsonSerializer.Serialize(request.Selector ?? string.Empty);
            var valueJson = JsonSerializer.Serialize(request.Text ?? string.Empty);
            var expression = $"(() => {{ const e = document.querySelector({selectorJson}); if (!e) return false; e.value = {valueJson}; e.dispatchEvent(new Event('input', {{ bubbles: true }})); e.dispatchEvent(new Event('change', {{ bubbles: true }})); return true; }})()";
            var result = await client.EvaluateAsync(expression, cancellationToken);
            return AutomationSupport.Success(request, result, ("backend", "cdp"));
        });
        if (cdpResult is not null)
        {
            return cdpResult;
        }

        return await ui.UiSetValueAsync(WithWebTarget(request), cancellationToken);
    }

    /// <summary>
    /// Routes a CDP call through the currently connected client, if any. If the client throws a
    /// transport failure (the browser tab/process went away, the socket dropped, etc.), the dead
    /// connection is invalidated here so subsequent calls fall back cleanly instead of repeating
    /// the same raw exception until the caller manually calls web_disconnect (BUG-2).
    /// </summary>
    /// <returns>
    /// The action's result; a structured "connection lost" failure if the call transport-failed;
    /// or null if there is no active CDP connection at all (letting the caller fall back to its
    /// non-CDP backend).
    /// </returns>
    private async Task<AutomationResult?> TryUseCdpAsync(AutomationRequest request, Func<CdpClient, Task<AutomationResult>> action)
    {
        var client = GetCdp();
        if (client is null)
        {
            return null;
        }

        try
        {
            return await action(client);
        }
        catch (Exception ex) when (ex is WebSocketException or IOException or ObjectDisposedException)
        {
            DisposeCdp();
            return AutomationSupport.Failure(
                request,
                $"The browser debug protocol connection was lost ({ex.Message}); call web_connect to reconnect.",
                ("backend", "cdp"),
                ("connectionLost", "true"));
        }
    }

    internal static Uri? ResolveCdpEndpoint(string? target)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return null;
        }

        var value = target!.Trim();
        if (int.TryParse(value, out var port) && port > 0)
        {
            return new Uri($"http://127.0.0.1:{port}");
        }

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
             absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            return absolute!;
        }

        return value.Contains(":")
            ? new Uri($"http://{value}")
            : null;
    }

    /// <summary>BUG-3: cdp/connectedWebTarget/connectedWebUrl are shared mutable state that
    /// concurrent broker calls (e.g. overlapping web_navigate and web_dom_get) can hit from
    /// different thread-pool threads with no marshaling to a single thread, unlike DTE-bound
    /// services. Guard every access with <see cref="stateLock"/>, mirroring the pattern
    /// EditorCapabilityService uses for pendingEdits/pendingEditLock.</summary>
    private CdpClient? GetCdp()
    {
        lock (stateLock)
        {
            return cdp;
        }
    }

    private string? GetConnectedWebUrl()
    {
        lock (stateLock)
        {
            return connectedWebUrl;
        }
    }

    private void DisposeCdp()
    {
        CdpClient? previous;
        lock (stateLock)
        {
            previous = cdp;
            cdp = null;
        }

        previous?.Dispose();
    }

    private static AutomationRequest WithWebTarget(AutomationRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Target))
        {
            return request;
        }

        request.Target = "chrome";
        return request;
    }

    private static IEnumerable<string> QueryHtml(string html, string? selector)
    {
        if (string.IsNullOrWhiteSpace(selector))
        {
            yield break;
        }

        var trimmed = (selector ?? string.Empty).Trim();
        var pattern = trimmed.StartsWith("#", StringComparison.Ordinal)
            ? $@"<[^>]+id\s*=\s*[""']{Regex.Escape(trimmed.Substring(1))}[""'][^>]*>"
            : trimmed.StartsWith(".", StringComparison.Ordinal)
                ? $@"<[^>]+class\s*=\s*[""'][^""']*{Regex.Escape(trimmed.Substring(1))}[^""']*[""'][^>]*>"
                : $@"<{Regex.Escape(trimmed)}(\s[^>]*)?>";

        foreach (Match match in Regex.Matches(html, pattern, RegexOptions.IgnoreCase))
        {
            yield return match.Value;
        }
    }

    private static string DownloadText(string url)
    {
        using var client = new WebClient();
        client.Encoding = Encoding.UTF8;
        return client.DownloadString(url);
    }
}
