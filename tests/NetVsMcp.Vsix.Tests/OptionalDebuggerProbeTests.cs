using System;
using System.Runtime.InteropServices;

namespace NetVsMcp.Vsix.Tests;

public class OptionalDebuggerProbeTests
{
    [Fact]
    public void OptionalEngineFailureDoesNotMasqueradeAsMissingSymbols()
    {
        var unsupported = OptionalDebuggerProbe.Read<string>(() => throw new NotImplementedException());
        Assert.False(unsupported.Supported);
        Assert.NotNull(unsupported.Reason);
        var missing = OptionalDebuggerProbe.Read<string?>(() => null);
        Assert.True(missing.Supported);
        Assert.Null(missing.Reason);
    }

    [Fact]
    public void HandlesComNotImplementedButPreservesUnexpectedFailures()
    {
        Assert.False(OptionalDebuggerProbe.Read<string>(() => throw new COMException("unsupported", unchecked((int)0x80004001))).Supported);
        Assert.Throws<InvalidOperationException>(() => OptionalDebuggerProbe.Read<string>(() => throw new InvalidOperationException("unexpected")));
    }
}
