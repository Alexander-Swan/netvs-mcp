using System;
using System.Reflection;
using System.Threading.Tasks;
using NetVsMcp.Contracts;
using StreamJsonRpc;

// The tested task is completed directly by the RPC boundary, not a joinable UI operation.
#pragma warning disable VSTHRD003

namespace NetVsMcp.Vsix.Tests;

public class RpcFailureTranslationTests
{
    [Theory]
    [InlineData("debugger", VsRpcErrorCodes.StaleDebuggerContext)]
    [InlineData("action", VsRpcErrorCodes.StaleAction)]
    [InlineData("arguments", VsRpcErrorCodes.InvalidRequest)]
    [InlineData("unsupported", VsRpcErrorCodes.FeatureUnavailable)]
    [InlineData("history", VsRpcErrorCodes.HistoryUnavailable)]
    [InlineData("inspection", VsRpcErrorCodes.InspectionUnavailable)]
    public async Task RpcBoundaryPreservesKnownFailureKinds(string kind, int expected)
    {
        Exception exception = kind switch
        {
            "debugger" => new StaleDebugContextException("stale pause"),
            "action" => new StaleActionException("stale action"),
            "arguments" => new ArgumentException("invalid arguments"),
            "history" => new HistoryUnavailableException("expired history"),
            "inspection" => new InspectionUnavailableException("frame unavailable"),
            _ => new NotSupportedException("unsupported engine")
        };
        Func<Task<string>> operation = () => Task.FromException<string>(exception);
        var method = typeof(VisualStudioCapabilityRpcTarget).GetMethod("InvokeAsync", BindingFlags.Static | BindingFlags.NonPublic)!.MakeGenericMethod(typeof(string));
        var task = (Task<string>)method.Invoke(null, new object[] { operation, "Probe" })!;
        var failure = await Assert.ThrowsAsync<LocalRpcException>(() => task);
        Assert.Equal(expected, failure.ErrorCode);
    }
}
