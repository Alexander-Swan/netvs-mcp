using System;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using NetVsMcp.Vsix;
using NetVsMcp.Vsix.Interfaces;

namespace NetVsMcp.Vsix.Tests;

public sealed class BrokerConnectionFactoryTests
{
    [Fact]
    public async Task ConnectAsync_RelaunchesBundledBroker_AfterPreviousLaunchedConnectionEnds()
    {
        var pipeName = "netvs-mcp-test-" + Guid.NewGuid().ToString("N");
        using var launcher = new TestBrokerLauncher(pipeName);
        var factory = new NamedPipeBrokerConnectionFactory(
            pipeName,
            new object(),
            new BrokerInstallationDetector(launcher),
            launcher);

        using (await factory.ConnectAsync(CancellationToken.None))
        {
            Assert.Equal(1, launcher.LaunchCount);
        }

        using (await factory.ConnectAsync(CancellationToken.None))
        {
            Assert.Equal(2, launcher.LaunchCount);
        }
    }

    private sealed class TestBrokerLauncher : IBrokerProcessLauncher, IDisposable
    {
        private readonly string pipeName;
        private readonly List<NamedPipeServerStream> servers = [];

        public TestBrokerLauncher(string pipeName)
        {
            this.pipeName = pipeName;
        }

        public bool IsAvailable => true;

        public int LaunchCount { get; private set; }

        public Task<bool> TryLaunchAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LaunchCount++;

            var server = new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                NamedPipeServerStream.MaxAllowedServerInstances,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
            servers.Add(server);
            _ = server.WaitForConnectionAsync(cancellationToken);
            return Task.FromResult(true);
        }

        public void Dispose()
        {
            foreach (var server in servers)
            {
                server.Dispose();
            }
        }
    }
}
