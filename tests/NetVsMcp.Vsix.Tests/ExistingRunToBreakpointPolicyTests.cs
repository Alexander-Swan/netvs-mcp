namespace NetVsMcp.Vsix.Tests;

public class ExistingRunToBreakpointPolicyTests
{
    [Theory]
    [InlineData(true, true, false, false, null, 123, true)]
    [InlineData(true, true, false, false, " ProcessID == 123 ", 123, true)]
    [InlineData(true, true, false, false, "processid==123", 123, true)]
    [InlineData(false, true, false, false, null, 123, false)]
    [InlineData(true, false, false, false, null, 123, false)]
    [InlineData(true, true, true, false, null, 123, false)]
    [InlineData(true, true, false, true, null, 123, false)]
    [InlineData(true, true, false, false, "ProcessID == 456", 123, false)]
    [InlineData(true, true, false, false, "ProcessID == 123 || true", 123, false)]
    [InlineData(true, true, false, false, null, 0, false)]
    public void OnlyUnconditionalStoppingBreakpointsForTheExpectedProcessCanBeReused(
        bool enabled, bool stopping, bool conditional, bool counted, string? filter, int processId, bool expected) =>
        Assert.Equal(expected, ExistingRunToBreakpointPolicy.CanReuse(enabled, stopping, conditional, counted, filter, processId));
}
