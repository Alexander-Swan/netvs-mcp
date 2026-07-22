using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetVsMcp.Broker.Services;

public sealed class LocalMcpHttpHost : IAsyncDisposable
{
    private readonly BrokerOptions _options;
    private readonly BrokerToolService _tools;
    private WebApplication? _application;

    public LocalMcpHttpHost(BrokerOptions options, BrokerToolService tools)
    {
        _options = options;
        _tools = tools;
    }

    public bool IsRunning => _application is not null;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_application is not null)
        {
            return;
        }

        var endpoint = ParseEndpoint(_options.McpEndpoint);
        var builder = WebApplication.CreateSlimBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(LocalMcpHttpHost).Assembly.FullName
        });

        builder.WebHost.ConfigureKestrel(server =>
        {
            server.Listen(IPAddress.Loopback, endpoint.Port, ListenOptions);
        });

        builder.Services.Configure<JsonOptions>(json =>
        {
            json.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
            json.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
        });

        var app = builder.Build();
        MapRoutes(app);

        await app.StartAsync(cancellationToken);
        _application = app;
    }

    public async Task StopAsync()
    {
        if (_application is null)
        {
            return;
        }

        await _application.StopAsync();
        await _application.DisposeAsync();
        _application = null;
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
    }

    private void MapRoutes(WebApplication app)
    {
        var tools = app.MapGroup("/mcp/tools");

        tools.MapGet("/vs_list_sessions", () => Results.Ok(_tools.VsListSessions()));
        tools.MapGet("/vs_get_status", () => Results.Ok(_tools.VsGetStatus()));
        tools.MapGet("/vs_get_capabilities", () => Results.Ok(_tools.VsGetCapabilities()));

        app.MapGet("/", () => Results.Ok(new
        {
            name = "NetVsMcp Broker",
            transport = "HTTP MCP placeholder",
            endpoint = _options.McpEndpoint,
            tools = new[]
            {
                "vs_list_sessions",
                "vs_get_status",
                "vs_get_capabilities"
            },
            todo = "Replace placeholder JSON routes with the Model Context Protocol HTTP transport."
        }));

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "running",
            endpoint = _options.McpEndpoint
        }));
    }

    private static Uri ParseEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid MCP endpoint '{endpoint}'.");
        }

        if (!IPAddress.IsLoopback(IPAddress.Parse(uri.Host)))
        {
            throw new InvalidOperationException("The broker HTTP endpoint must bind to a loopback address.");
        }

        return uri;
    }

    private static void ListenOptions(ListenOptions options)
    {
        options.Protocols = HttpProtocols.Http1AndHttp2;
    }
}
