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

    public async Task<AutomationResult> WebBlazorInspectAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var result = await client.EvaluateAsync(BlazorInspectExpression, cancellationToken);
            var metadata = ParseBlazorInspectionMetadata(result);
            return AutomationSupport.Success(
                request,
                AutomationSupport.Truncate(result),
                ("backend", "cdp"),
                ("detected", metadata.Detected),
                ("hostingModel", metadata.HostingModel),
                ("scriptCount", metadata.ScriptCount),
                ("frameworkResourceCount", metadata.FrameworkResourceCount));
        });

        return cdpResult
            ?? AutomationSupport.Failure(request, "Blazor inspection requires a connected browser debug protocol backend; call web_connect with a CDP endpoint first.", ("backend", "browser-shell-uia"));
    }

    public async Task<AutomationResult> WebBlazorWaitReadyAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var result = await client.EvaluateAsync(BuildBlazorWaitReadyExpression(request.TimeoutMilliseconds), cancellationToken);
            return AutomationSupport.Success(
                request,
                AutomationSupport.Truncate(result),
                ("backend", "cdp"),
                ("ready", TryReadJsonProperty(result, "ready")),
                ("hostingModel", TryReadJsonProperty(result, "hostingModel")),
                ("timedOut", TryReadJsonProperty(result, "timedOut")));
        });

        return cdpResult
            ?? AutomationSupport.Failure(request, "Blazor readiness checks require a connected browser debug protocol backend; call web_connect with a CDP endpoint first.", ("backend", "browser-shell-uia"));
    }

    public async Task<AutomationResult> WebBlazorGetComponentsAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var result = await client.EvaluateAsync(BlazorGetComponentsExpression, cancellationToken);
            return AutomationSupport.Success(
                request,
                AutomationSupport.Truncate(result),
                ("backend", "cdp"),
                ("componentMarkerCount", TryReadJsonProperty(result, "componentMarkerCount")),
                ("commentMarkerCount", TryReadJsonProperty(result, "commentMarkerCount")));
        });

        return cdpResult
            ?? AutomationSupport.Failure(request, "Blazor component inspection requires a connected browser debug protocol backend; call web_connect with a CDP endpoint first.", ("backend", "browser-shell-uia"));
    }

    public async Task<AutomationResult> WebBlazorGetStateAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var result = await client.EvaluateAsync(BuildBlazorGetStateExpression(request.Selector), cancellationToken);
            return AutomationSupport.Success(
                request,
                AutomationSupport.Truncate(result),
                ("backend", "cdp"),
                ("found", TryReadJsonProperty(result, "found")),
                ("controlCount", TryReadJsonProperty(result, "controlCount")),
                ("statusCount", TryReadJsonProperty(result, "statusCount")));
        });

        return cdpResult
            ?? AutomationSupport.Failure(request, "Blazor state inspection requires a connected browser debug protocol backend; call web_connect with a CDP endpoint first.", ("backend", "browser-shell-uia"));
    }

    public async Task<AutomationResult> WebBlazorTriggerEventAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var result = await client.EvaluateAsync(BuildBlazorTriggerEventExpression(request.Selector, request.Text), cancellationToken);
            return AutomationSupport.Success(
                request,
                AutomationSupport.Truncate(result),
                ("backend", "cdp"),
                ("found", TryReadJsonProperty(result, "found")),
                ("dispatched", TryReadJsonProperty(result, "dispatched")),
                ("eventName", TryReadJsonProperty(result, "eventName")));
        });

        return cdpResult
            ?? AutomationSupport.Failure(request, "Blazor event dispatch requires a connected browser debug protocol backend; call web_connect with a CDP endpoint first.", ("backend", "browser-shell-uia"));
    }

    public async Task<AutomationResult> WebBlazorInspectCircuitAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var result = await client.EvaluateAsync(BlazorInspectCircuitExpression, cancellationToken);
            return AutomationSupport.Success(
                request,
                AutomationSupport.Truncate(result),
                ("backend", "cdp"),
                ("detected", TryReadJsonProperty(result, "detected")),
                ("serverDescriptorCount", TryReadJsonProperty(result, "serverDescriptorCount")),
                ("blazorEndpointResourceCount", TryReadJsonProperty(result, "blazorEndpointResourceCount")));
        });

        return cdpResult
            ?? AutomationSupport.Failure(request, "Blazor circuit inspection requires a connected browser debug protocol backend; call web_connect with a CDP endpoint first.", ("backend", "browser-shell-uia"));
    }

    public async Task<AutomationResult> WebBlazorInspectWasmAsync(AutomationRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var cdpResult = await TryUseCdpAsync(request, async client =>
        {
            var result = await client.EvaluateAsync(BlazorInspectWasmExpression, cancellationToken);
            return AutomationSupport.Success(
                request,
                AutomationSupport.Truncate(result),
                ("backend", "cdp"),
                ("detected", TryReadJsonProperty(result, "detected")),
                ("wasmResourceCount", TryReadJsonProperty(result, "wasmResourceCount")),
                ("bootResourceCount", TryReadJsonProperty(result, "bootResourceCount")));
        });

        return cdpResult
            ?? AutomationSupport.Failure(request, "Blazor WebAssembly inspection requires a connected browser debug protocol backend; call web_connect with a CDP endpoint first.", ("backend", "browser-shell-uia"));
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

    private const string BlazorInspectExpression = """
        (() => {
          const scripts = Array.from(document.scripts)
            .map(s => s.src || s.getAttribute('src') || '')
            .filter(Boolean);
          const resources = performance.getEntriesByType('resource')
            .map(e => e.name || '')
            .filter(Boolean);
          const allUrls = scripts.concat(resources);
          const hasBlazorGlobal = !!window.Blazor;
          const componentMarkerCount = document.querySelectorAll('[blazor\\:id],[_bl_]').length;
          const frameworkResourceCount = allUrls.filter(u => u.includes('/_framework/') || u.includes('\\_framework\\')).length;
          const hasServerScript = allUrls.some(u => /_framework\/blazor(\.web)?\.js/i.test(u) || /_framework\/blazor\.server\.js/i.test(u));
          const hasServerCircuit = allUrls.some(u => /\/_blazor(\?|\/|$)/i.test(u));
          const hasWasmResource = allUrls.some(u =>
            /_framework\/blazor\.webassembly\.js/i.test(u) ||
            /_framework\/dotnet(\..*)?\.js/i.test(u) ||
            /_framework\/dotnet\.wasm/i.test(u) ||
            /_framework\/blazor\.boot\.json/i.test(u) ||
            /\.wasm(\?|$)/i.test(u));
          const detected = hasBlazorGlobal || componentMarkerCount > 0 || frameworkResourceCount > 0 || hasServerCircuit;
          const hostingModel = hasWasmResource
            ? 'webassembly'
            : hasServerCircuit || (hasServerScript && !hasWasmResource)
              ? 'server'
              : detected
                ? 'unknown'
                : 'none';
          return JSON.stringify({
            detected,
            hostingModel,
            hasBlazorGlobal,
            componentMarkerCount,
            scriptCount: scripts.length,
            frameworkResourceCount,
            sampleScripts: scripts.filter(u => u.includes('_framework') || u.includes('_blazor')).slice(0, 10),
            sampleFrameworkResources: allUrls.filter(u => u.includes('_framework') || u.includes('_blazor')).slice(0, 20),
            url: location.href,
            title: document.title
          });
        })()
        """;

    private const string BlazorGetComponentsExpression = """
        (() => {
          const commentMarkers = [];
          const walker = document.createTreeWalker(document, NodeFilter.SHOW_COMMENT);
          while (walker.nextNode()) {
            const value = walker.currentNode.nodeValue || '';
            if (value.trim().startsWith('Blazor:')) {
              commentMarkers.push(value.trim().slice(0, 500));
            }
          }
          const markerElements = Array.from(document.querySelectorAll('*'))
            .map((element) => {
              const attrs = Array.from(element.attributes || [])
                .map(a => a.name)
                .filter(name => name.startsWith('_bl_') || name.startsWith('blazor:'));
              if (attrs.length === 0) return null;
              return {
                tagName: element.tagName.toLowerCase(),
                id: element.id || '',
                attributes: attrs,
                text: (element.innerText || element.textContent || '').trim().slice(0, 200)
              };
            })
            .filter(Boolean);
          return JSON.stringify({
            componentMarkerCount: markerElements.length,
            commentMarkerCount: commentMarkers.length,
            markerElements: markerElements.slice(0, 100),
            commentMarkers: commentMarkers.slice(0, 100),
            url: location.href
          });
        })()
        """;

    private const string BlazorInspectCircuitExpression = """
        (() => {
          const resources = performance.getEntriesByType('resource')
            .map(e => e.name || '')
            .filter(Boolean);
          const scripts = Array.from(document.scripts)
            .map(s => s.src || s.getAttribute('src') || '')
            .filter(Boolean);
          const allUrls = scripts.concat(resources);
          const commentMarkers = [];
          const walker = document.createTreeWalker(document, NodeFilter.SHOW_COMMENT);
          while (walker.nextNode()) {
            const value = walker.currentNode.nodeValue || '';
            if (value.trim().startsWith('Blazor:')) commentMarkers.push(value.trim());
          }
          const serverDescriptors = commentMarkers.filter(value => /"type"\s*:\s*"server"/i.test(value));
          const blazorEndpointResources = allUrls.filter(url => /\/_blazor(\?|\/|$)/i.test(url));
          const serverScripts = allUrls.filter(url =>
            !/_framework\/blazor\.webassembly/i.test(url) &&
            /_framework\/blazor(?:\.server|\.web)?(?:\.[^\/]+)?\.js(?:\?|$)/i.test(url));
          const detected = serverDescriptors.length > 0 || blazorEndpointResources.length > 0 || serverScripts.length > 0;
          return JSON.stringify({
            detected,
            serverDescriptorCount: serverDescriptors.length,
            blazorEndpointResourceCount: blazorEndpointResources.length,
            serverScriptCount: serverScripts.length,
            serverDescriptors: serverDescriptors.slice(0, 20),
            blazorEndpointResources: blazorEndpointResources.slice(0, 20),
            serverScripts: serverScripts.slice(0, 20),
            url: location.href
          });
        })()
        """;

    private const string BlazorInspectWasmExpression = """
        (() => {
          const resources = performance.getEntriesByType('resource')
            .map(e => e.name || '')
            .filter(Boolean);
          const scripts = Array.from(document.scripts)
            .map(s => s.src || s.getAttribute('src') || '')
            .filter(Boolean);
          const allUrls = scripts.concat(resources);
          const wasmResources = allUrls.filter(url => /\.wasm(\?|$)/i.test(url) || /_framework\/dotnet\.wasm/i.test(url));
          const bootResources = allUrls.filter(url => /_framework\/blazor\.boot\.json/i.test(url));
          const dotnetScripts = allUrls.filter(url => /_framework\/dotnet(\..*)?\.js/i.test(url));
          const wasmScripts = allUrls.filter(url => /_framework\/blazor\.webassembly(\.[^\/]+)?\.js/i.test(url));
          const detected = wasmResources.length > 0 || bootResources.length > 0 || dotnetScripts.length > 0 || wasmScripts.length > 0;
          return JSON.stringify({
            detected,
            wasmResourceCount: wasmResources.length,
            bootResourceCount: bootResources.length,
            dotnetScriptCount: dotnetScripts.length,
            webAssemblyScriptCount: wasmScripts.length,
            wasmResources: wasmResources.slice(0, 20),
            bootResources: bootResources.slice(0, 20),
            dotnetScripts: dotnetScripts.slice(0, 20),
            webAssemblyScripts: wasmScripts.slice(0, 20),
            url: location.href
          });
        })()
        """;

    private static string BuildBlazorWaitReadyExpression(int timeoutMilliseconds) =>
        $$"""
        new Promise(resolve => {
          const timeout = {{Math.Max(1, timeoutMilliseconds)}};
          const started = Date.now();
          const inspect = () => {
            const resources = performance.getEntriesByType('resource').map(e => e.name || '').filter(Boolean);
            const scripts = Array.from(document.scripts).map(s => s.src || s.getAttribute('src') || '').filter(Boolean);
            const allUrls = scripts.concat(resources);
            const hasBlazorGlobal = !!window.Blazor;
            const hasComponentComments = (() => {
              const walker = document.createTreeWalker(document, NodeFilter.SHOW_COMMENT);
              while (walker.nextNode()) {
                if ((walker.currentNode.nodeValue || '').trim().startsWith('Blazor:')) return true;
              }
              return false;
            })();
            const hasFrameworkResource = allUrls.some(url => url.includes('/_framework/') || url.includes('\\_framework\\'));
            const hasServerCircuit = allUrls.some(url => /\/_blazor(\?|\/|$)/i.test(url));
            const hasWasmResource = allUrls.some(url => /_framework\/blazor\.webassembly/i.test(url) || /\.wasm(\?|$)/i.test(url) || /_framework\/blazor\.boot\.json/i.test(url));
            const ready = document.readyState !== 'loading' && (hasBlazorGlobal || hasComponentComments || hasFrameworkResource || hasServerCircuit);
            const hostingModel = hasWasmResource ? 'webassembly' : hasServerCircuit || hasComponentComments ? 'server' : ready ? 'unknown' : 'none';
            return {
              ready,
              timedOut: Date.now() - started >= timeout,
              elapsedMilliseconds: Date.now() - started,
              hostingModel,
              documentReadyState: document.readyState,
              hasBlazorGlobal,
              hasComponentComments,
              hasFrameworkResource,
              hasServerCircuit,
              hasWasmResource,
              url: location.href
            };
          };
          const tick = () => {
            const state = inspect();
            if (state.ready || state.timedOut) {
              resolve(JSON.stringify(state));
              return;
            }
            setTimeout(tick, 50);
          };
          tick();
        })
        """;

    private static string BuildBlazorGetStateExpression(string? selector)
    {
        var selectorJson = JsonSerializer.Serialize(selector);
        return $$"""
        (() => {
          const selector = {{selectorJson}};
          const root = selector ? document.querySelector(selector) : document.body;
          if (!root) {
            return JSON.stringify({ found: false, selector, url: location.href });
          }
          const controls = Array.from(root.querySelectorAll('input, textarea, select'))
            .map(element => ({
              tagName: element.tagName.toLowerCase(),
              id: element.id || '',
              name: element.name || '',
              type: element.type || '',
              value: element.value ?? '',
              checked: !!element.checked
            }));
          const statuses = Array.from(root.querySelectorAll('[role="status"], [aria-live]'))
            .map(element => ({
              tagName: element.tagName.toLowerCase(),
              id: element.id || '',
              text: (element.innerText || element.textContent || '').trim()
            }));
          const dataAttributes = Object.fromEntries(
            Array.from(root.attributes || [])
              .filter(attribute => attribute.name.startsWith('data-'))
              .map(attribute => [attribute.name, attribute.value]));
          const markerAttributeCount = Array.from(root.querySelectorAll('*'))
            .filter(element => Array.from(element.attributes || []).some(attribute => attribute.name.startsWith('_bl_') || attribute.name.startsWith('blazor:')))
            .length;
          return JSON.stringify({
            found: true,
            selector,
            tagName: root.tagName ? root.tagName.toLowerCase() : 'document',
            id: root.id || '',
            text: (root.innerText || root.textContent || '').trim().slice(0, 1000),
            dataAttributes,
            controlCount: controls.length,
            statusCount: statuses.length,
            markerAttributeCount,
            controls: controls.slice(0, 100),
            statuses: statuses.slice(0, 50),
            url: location.href
          });
        })()
        """;
    }

    private static string BuildBlazorTriggerEventExpression(string? selector, string? eventName)
    {
        var selectorJson = JsonSerializer.Serialize(selector ?? string.Empty);
        var eventNameJson = JsonSerializer.Serialize(eventName ?? string.Empty);
        return $$"""
        (() => {
          const selector = {{selectorJson}};
          const eventName = {{eventNameJson}}.trim();
          const element = document.querySelector(selector);
          if (!element || !eventName) {
            return JSON.stringify({ found: !!element, dispatched: false, selector, eventName, url: location.href });
          }
          let dispatched = false;
          if (eventName === 'click' && typeof element.click === 'function') {
            element.click();
            dispatched = true;
          } else {
            const lower = eventName.toLowerCase();
            const event = lower.startsWith('key')
              ? new KeyboardEvent(eventName, { bubbles: true, cancelable: true })
              : lower.startsWith('mouse')
                ? new MouseEvent(eventName, { bubbles: true, cancelable: true })
                : new Event(eventName, { bubbles: true, cancelable: true });
            dispatched = element.dispatchEvent(event);
          }
          return JSON.stringify({
            found: true,
            dispatched,
            selector,
            eventName,
            tagName: element.tagName.toLowerCase(),
            id: element.id || '',
            text: (element.innerText || element.textContent || '').trim().slice(0, 200),
            url: location.href
          });
        })()
        """;
    }

    private static BlazorInspectionMetadata ParseBlazorInspectionMetadata(string result)
    {
        try
        {
            using var document = JsonDocument.Parse(result);
            var root = document.RootElement;
            return new BlazorInspectionMetadata(
                GetJsonProperty(root, "detected"),
                GetJsonProperty(root, "hostingModel"),
                GetJsonProperty(root, "scriptCount"),
                GetJsonProperty(root, "frameworkResourceCount"));
        }
        catch (JsonException)
        {
            return new BlazorInspectionMetadata("unknown", "unknown", "unknown", "unknown");
        }
    }

    private static string GetJsonProperty(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) ? property.ToString() : string.Empty;

    private static string TryReadJsonProperty(string json, string propertyName)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            return GetJsonProperty(document.RootElement, propertyName);
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }
}

internal sealed class BlazorInspectionMetadata
{
    public BlazorInspectionMetadata(
        string detected,
        string hostingModel,
        string scriptCount,
        string frameworkResourceCount)
    {
        Detected = detected;
        HostingModel = hostingModel;
        ScriptCount = scriptCount;
        FrameworkResourceCount = frameworkResourceCount;
    }

    public string Detected { get; }
    public string HostingModel { get; }
    public string ScriptCount { get; }
    public string FrameworkResourceCount { get; }
}
