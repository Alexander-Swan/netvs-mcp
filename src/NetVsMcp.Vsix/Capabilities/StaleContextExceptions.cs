using System;

namespace NetVsMcp.Vsix;

internal sealed class StaleDebugContextException(string message) : InvalidOperationException(message);
internal sealed class StaleActionException(string message) : InvalidOperationException(message);
internal sealed class HistoryUnavailableException(string message) : InvalidOperationException(message);
