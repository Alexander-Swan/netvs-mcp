using System;
using System.Collections.Generic;
using System.Reflection;
using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

/// <summary>
/// Covers pure helper logic used by the web-debug/console automation capability services
/// (endpoint parsing, CDP target selection, console buffer formatting) that has no VS/COM/network
/// dependency. The former monolithic
/// AutomationCapabilityService, moving <c>CdpClient</c>/<c>CdpTarget</c> to top-level internal
/// types (CdpClient.cs) and <c>ResolveCdpEndpoint</c> to an internal static method on
/// WebDebugCapabilityService, both visible here via InternalsVisibleTo; FormatConsoleBuffer
/// remains private on ConsoleAutomationCapabilityService, so it's still invoked via reflection.
/// </summary>
public class AutomationCapabilityServiceHelperTests
{
    private static readonly Type ConsoleServiceType = typeof(ConsoleAutomationCapabilityService);

    private static Uri? ResolveCdpEndpoint(string? target) =>
        WebDebugCapabilityService.ResolveCdpEndpoint(target);

    private static string FormatConsoleBuffer(string raw, int width)
    {
        var method = ConsoleServiceType.GetMethod("FormatConsoleBuffer", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FormatConsoleBuffer method not found.");
        return (string)method.Invoke(null, new object?[] { raw, width })!;
    }

    private static CdpTarget CreateCdpTarget(string? url, string? title, string? type, Uri webSocketUri) =>
        new(url, title, type, webSocketUri);

    private static CdpTarget? SelectTarget(IReadOnlyList<CdpTarget> targets, string? requestedUrl) =>
        CdpClient.SelectTarget(targets, requestedUrl);

    private static string? GetUrl(CdpTarget cdpTarget) => cdpTarget.Url;

    [Theory]
    [InlineData("9222", "http://127.0.0.1:9222/")]
    [InlineData("  9222  ", "http://127.0.0.1:9222/")]
    public void ResolveCdpEndpoint_BarePort_ResolvesToLocalhost(string input, string expected)
    {
        var result = ResolveCdpEndpoint(input);
        Assert.Equal(new Uri(expected), result);
    }

    [Fact]
    public void ResolveCdpEndpoint_HostAndPort_ResolvesToHttp()
    {
        var result = ResolveCdpEndpoint("example.com:9222");
        Assert.Equal(new Uri("http://example.com:9222"), result);
    }

    [Fact]
    public void ResolveCdpEndpoint_FullHttpUrl_PassesThrough()
    {
        var result = ResolveCdpEndpoint("http://localhost:9222/devtools");
        Assert.Equal(new Uri("http://localhost:9222/devtools"), result);
    }

    [Fact]
    public void ResolveCdpEndpoint_FullHttpsUrl_PassesThrough()
    {
        var result = ResolveCdpEndpoint("https://localhost:9222/devtools");
        Assert.Equal(new Uri("https://localhost:9222/devtools"), result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveCdpEndpoint_NullOrWhitespace_ReturnsNull(string? input)
    {
        Assert.Null(ResolveCdpEndpoint(input));
    }

    [Fact]
    public void ResolveCdpEndpoint_PlainHostNoPort_ReturnsNull()
    {
        // No port and not a full http(s) URL -- nothing to resolve to.
        Assert.Null(ResolveCdpEndpoint("localhost"));
    }

    [Fact]
    public void ResolveCdpEndpoint_ZeroPort_IsNotTreatedAsPort()
    {
        // port > 0 is required, and "0" has no ':' either, so this falls through to null.
        Assert.Null(ResolveCdpEndpoint("0"));
    }

    [Fact]
    public void SelectTarget_EmptyCollection_ReturnsNull()
    {
        var result = SelectTarget(Array.Empty<CdpTarget>(), null);
        Assert.Null(result);
    }

    [Fact]
    public void SelectTarget_NoRequestedUrl_PrefersPageType()
    {
        var iframe = CreateCdpTarget("http://a", "A", "iframe", new Uri("ws://x/1"));
        var page = CreateCdpTarget("http://b", "B", "page", new Uri("ws://x/2"));

        var result = SelectTarget(new[] { iframe, page }, null);

        Assert.Equal("http://b", GetUrl(result!));
    }

    [Fact]
    public void SelectTarget_NoRequestedUrl_NoPageType_FallsBackToFirst()
    {
        var first = CreateCdpTarget("http://a", "A", "iframe", new Uri("ws://x/1"));
        var second = CreateCdpTarget("http://b", "B", "background_page", new Uri("ws://x/2"));

        var result = SelectTarget(new[] { first, second }, null);

        Assert.Equal("http://a", GetUrl(result!));
    }

    [Fact]
    public void SelectTarget_RequestedUrl_MatchesByUrlSubstring()
    {
        var page = CreateCdpTarget("http://localhost/page", "Page", "page", new Uri("ws://x/1"));
        var other = CreateCdpTarget("http://example.com/target", "Example", "page", new Uri("ws://x/2"));

        var result = SelectTarget(new[] { page, other }, "example.com");

        Assert.Equal("http://example.com/target", GetUrl(result!));
    }

    [Fact]
    public void SelectTarget_RequestedUrl_MatchesByTitleSubstring()
    {
        var page = CreateCdpTarget("http://localhost/a", "Widgets Dashboard", "page", new Uri("ws://x/1"));
        var other = CreateCdpTarget("http://localhost/b", "Other", "page", new Uri("ws://x/2"));

        var result = SelectTarget(new[] { page, other }, "widgets");

        Assert.Equal("http://localhost/a", GetUrl(result!));
    }

    [Fact]
    public void SelectTarget_RequestedUrl_NoMatch_FallsBackToPageType()
    {
        var page = CreateCdpTarget("http://localhost/a", "A", "page", new Uri("ws://x/1"));

        var result = SelectTarget(new[] { page }, "not-found-anywhere");

        Assert.Equal("http://localhost/a", GetUrl(result!));
    }

    [Fact]
    public void FormatConsoleBuffer_ShortText_ReturnsSingleTrimmedLine()
    {
        var result = FormatConsoleBuffer("hello   ", 80);
        Assert.Equal("hello", result);
    }

    [Fact]
    public void FormatConsoleBuffer_WrapsAtWidth()
    {
        var raw = new string('a', 10) + new string('b', 10);
        var result = FormatConsoleBuffer(raw, 10);

        Assert.Equal(new string('a', 10) + Environment.NewLine + new string('b', 10), result);
    }

    [Fact]
    public void FormatConsoleBuffer_TrimsTrailingWhitespacePerLine()
    {
        var raw = "abc       " + "def       ";
        var result = FormatConsoleBuffer(raw, 10);

        Assert.Equal("abc" + Environment.NewLine + "def", result);
    }

    [Fact]
    public void FormatConsoleBuffer_EmptyInput_ReturnsEmpty()
    {
        var result = FormatConsoleBuffer(string.Empty, 80);
        Assert.Equal(string.Empty, result);
    }
}
