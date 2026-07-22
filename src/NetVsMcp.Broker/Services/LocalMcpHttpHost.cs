using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Server;
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
        builder.Services.AddSingleton(_tools);
        builder.Services
            .AddMcpServer()
            .WithHttpTransport(options =>
            {
                options.Stateless = true;
            })
            .WithTools<BrokerToolService>(_tools);

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
        app.MapGet("/", () => Results.Ok(new
        {
            name = "NetVsMcp Broker",
            transport = "MCP Streamable HTTP",
            endpoint = _options.McpEndpoint,
            mcp = "/mcp",
            health = "/health"
        }));

        app.MapGet("/health", () => Results.Ok(new
        {
            status = "running",
            endpoint = _options.McpEndpoint
        }));

        app.MapMcp("/mcp");
    }

    private static Uri ParseEndpoint(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid MCP endpoint '{endpoint}'.");
        }

        if (!IsLoopbackHost(uri.Host))
        {
            throw new InvalidOperationException("The broker HTTP endpoint must bind to a loopback address.");
        }

        return uri;
    }

    private static bool IsLoopbackHost(string host)
    {
        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }

    private static void ListenOptions(ListenOptions options)
    {
        options.Protocols = HttpProtocols.Http1AndHttp2;
    }
}
