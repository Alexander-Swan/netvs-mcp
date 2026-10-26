using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    // Keep identity once per public response, and only when explicitly requested.
    internal static ToolResponse<T> ShapeInspectionOutput<T>(ToolResponse<T> response, bool includeContext, bool compact = false, string? solutionPath = null)
    {
        if (response.Value is null) return response;
        object value = response.Value switch
        {
            LocalsResult r => r with { Context = includeContext ? r.Context : null },
            CallStackResult r => r with { Context = includeContext ? r.Context : null },
            EvaluateExpressionResult r => r with { Context = includeContext ? r.Context : null },
            WatchListResult r => r with { Context = includeContext ? r.Context : null },
            WatchOperationResult r => r with { Context = includeContext ? r.Context : null },
            DebugSetVariableResult r => r with { Evaluation = r.Evaluation is null ? null : r.Evaluation with { Context = includeContext ? r.Evaluation.Context : null } },
            DebugEvalManyResult r => r with { Context = includeContext ? r.Context : null,
                Results = r.Results.Select(item => item with { Context = null }).ToArray() },
            DebugFrameInspectionResult r => r with { Context = includeContext ? r.Context ?? r.Expansion?.Context : null,
                Expansion = r.Expansion is null ? null : r.Expansion with { Context = null },
                Locals = r.Locals is null ? null : r.Locals with { Context = null },
                CallStack = r.CallStack is null ? null : r.CallStack with { Context = null },
                Watch = r.Watch is null ? null : r.Watch with { Context = null },
                Results = r.Results.Select(item => item with { Context = null }).ToArray() },
            DebugSnapshotResult r => r with { Context = includeContext ? r.Context ?? r.Locals?.Context ?? r.CallStack?.Context ?? r.Watch?.Context : null,
                Locals = r.Locals is null ? null : r.Locals with { Context = null },
                CallStack = r.CallStack is null ? null : r.CallStack with { Context = null },
                Watch = r.Watch is null ? null : r.Watch with { Context = null } },
            _ => response.Value
        };
        if (compact) value = value switch
        {
            DebugSnapshotResult r => r with
            {
                CallStack = r.CallStack is null ? null : r.CallStack with
                {
                    Frames = r.CallStack.Frames.Select(frame => frame with { File = CompactSourcePath(frame.File, solutionPath) }).ToArray()
                }
            },
            _ => value
        };
        return response with { Value = (T)value };
    }

    private static string? CompactSourcePath(string? file, string? solutionPath)
    {
        if (file is null || solutionPath is null || !Path.IsPathRooted(file) || !Path.IsPathRooted(solutionPath)) return file;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(solutionPath));
            if (directory is null) return file;
            var relative = Path.GetRelativePath(directory, Path.GetFullPath(file));
            return Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                ? file : relative.Replace('\\', '/');
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException) { return file; }
    }
}
