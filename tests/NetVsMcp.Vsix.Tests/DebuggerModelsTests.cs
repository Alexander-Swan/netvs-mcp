using System;
using System.Reflection;
using System.Text.Json;
using EnvDTE;

namespace NetVsMcp.Vsix.Tests;

/// <summary>
/// Covers breakpoint/hit-count metadata mapping in DebuggerModels.cs (<c>BreakpointMetadata</c>) and
/// DebuggerCapabilityService.cs (<c>ResolveHitCountType</c>) -- untested pure logic adjacent to a
/// historical breakpoint set/remove bug.
///
/// <c>BreakpointMetadata</c> lives in DebuggerModels.cs, which is fair game to edit, but its members
/// are already public on an internal class so no changes were needed. <c>ResolveHitCountType</c> lives
/// in DebuggerCapabilityService.cs and is private, so it's reached via reflection instead of widening
/// its visibility.
/// </summary>
public class DebuggerModelsTests
{
    [Fact]
    public void BreakpointMetadata_FromRequest_MapsAllFields()
    {
        var request = new BreakpointSetRequest
        {
            Action = "Print",
            ActionMessage = "hit {n}",
            ContinueAfterAction = true,
            HitCount = 3,
            HitCountType = "multiple",
            DependsOnBreakpointName = "OtherBp",
            GroupName = "MyGroup"
        };

        var metadata = BreakpointMetadata.FromRequest(request);

        Assert.Equal("Print", metadata.Action);
        Assert.Equal("hit {n}", metadata.ActionMessage);
        Assert.True(metadata.ContinueAfterAction);
        Assert.Equal(3, metadata.HitCount);
        Assert.Equal("multiple", metadata.HitCountType);
        Assert.Equal("OtherBp", metadata.DependsOnBreakpointName);
        Assert.Equal("MyGroup", metadata.GroupName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BreakpointMetadata_FromRequest_TreatsBlankStringsAsNull(string? blank)
    {
        var request = new BreakpointSetRequest
        {
            Action = blank,
            ActionMessage = blank,
            HitCountType = blank,
            DependsOnBreakpointName = blank,
            GroupName = blank
        };

        var metadata = BreakpointMetadata.FromRequest(request);

        Assert.Null(metadata.Action);
        Assert.Null(metadata.ActionMessage);
        Assert.Null(metadata.HitCountType);
        Assert.Null(metadata.DependsOnBreakpointName);
        Assert.Null(metadata.GroupName);
    }

    [Fact]
    public void BreakpointMetadata_FromRequest_PreservesFalseContinueAfterActionAndNullHitCount()
    {
        var request = new BreakpointSetRequest();

        var metadata = BreakpointMetadata.FromRequest(request);

        Assert.False(metadata.ContinueAfterAction);
        Assert.Null(metadata.HitCount);
    }

    [Fact]
    public void BreakpointMetadata_JsonRoundTrip_PreservesAllFields()
    {
        // Mirrors the shape ApplyTo/TryReadTag rely on when stuffing/reading metadata via the
        // breakpoint's Tag property (see DebuggerModels.cs).
        var original = new BreakpointMetadata
        {
            Action = "Print",
            ActionMessage = "hit",
            ContinueAfterAction = true,
            HitCount = 5,
            HitCountType = "equal",
            DependsOnBreakpointName = "Other",
            GroupName = "Group"
        };

        var json = JsonSerializer.Serialize(original);
        var roundTripped = JsonSerializer.Deserialize<BreakpointMetadata>(json);

        Assert.NotNull(roundTripped);
        Assert.Equal(original.Action, roundTripped!.Action);
        Assert.Equal(original.ActionMessage, roundTripped.ActionMessage);
        Assert.Equal(original.ContinueAfterAction, roundTripped.ContinueAfterAction);
        Assert.Equal(original.HitCount, roundTripped.HitCount);
        Assert.Equal(original.HitCountType, roundTripped.HitCountType);
        Assert.Equal(original.DependsOnBreakpointName, roundTripped.DependsOnBreakpointName);
        Assert.Equal(original.GroupName, roundTripped.GroupName);
    }

    private static dbgHitCountType ResolveHitCountType(string? hitCountType, int hitCount)
    {
        var method = typeof(DebuggerCapabilityService).GetMethod(
            "ResolveHitCountType",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("ResolveHitCountType method not found.");

        try
        {
            return (dbgHitCountType)method.Invoke(null, new object?[] { hitCountType, hitCount })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            throw ex.InnerException;
        }
    }

    [Fact]
    public void ResolveHitCountType_ZeroOrNegativeHitCount_ReturnsNone()
    {
        Assert.Equal(dbgHitCountType.dbgHitCountTypeNone, ResolveHitCountType("equal", 0));
        Assert.Equal(dbgHitCountType.dbgHitCountTypeNone, ResolveHitCountType(null, -1));
    }

    [Fact]
    public void ResolveHitCountType_NullOrWhitespaceType_DefaultsToEqual()
    {
        Assert.Equal(dbgHitCountType.dbgHitCountTypeEqual, ResolveHitCountType(null, 5));
        Assert.Equal(dbgHitCountType.dbgHitCountTypeEqual, ResolveHitCountType("   ", 5));
    }

    [Theory]
    [InlineData("equal")]
    [InlineData("equals")]
    [InlineData("exact")]
    [InlineData("==")]
    [InlineData("EQUAL")]
    [InlineData("  equal  ")]
    public void ResolveHitCountType_EqualAliases_ResolveToEqual(string alias)
    {
        Assert.Equal(dbgHitCountType.dbgHitCountTypeEqual, ResolveHitCountType(alias, 5));
    }

    [Theory]
    [InlineData("multiple")]
    [InlineData("multipleof")]
    [InlineData("multiple_of")]
    public void ResolveHitCountType_MultipleAliases_ResolveToMultiple(string alias)
    {
        Assert.Equal(dbgHitCountType.dbgHitCountTypeMultiple, ResolveHitCountType(alias, 5));
    }

    [Theory]
    [InlineData("greaterthanorequal")]
    [InlineData("greater_than_or_equal")]
    [InlineData("greater-or-equal")]
    [InlineData(">=")]
    public void ResolveHitCountType_GreaterOrEqualAliases_ResolveToGreaterOrEqual(string alias)
    {
        Assert.Equal(dbgHitCountType.dbgHitCountTypeGreaterOrEqual, ResolveHitCountType(alias, 5));
    }

    [Fact]
    public void ResolveHitCountType_UnknownType_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() => ResolveHitCountType("bogus", 5));
        Assert.Equal("hitCountType", ex.ParamName);
    }
}
