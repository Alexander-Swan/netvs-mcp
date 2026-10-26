using NetVsMcp.Contracts;

namespace NetVsMcp.Broker.Services;

internal sealed partial class BrokerToolService
{
    // Keep identity once per public response, and only when explicitly requested.
    private static ToolResponse<T> ShapeInspectionOutput<T>(ToolResponse<T> response, bool includeContext)
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
        return response with { Value = (T)value };
    }
}
