using System;
using System.Runtime.InteropServices;

namespace NetVsMcp.Vsix;

internal static class OptionalDebuggerProbe
{
    public static (bool Supported, T? Value, string? Reason) Read<T>(Func<T> read)
    {
        try { return (true, read(), null); }
        catch (Exception ex) when (ex is NotImplementedException or NotSupportedException ||
            ex is COMException { HResult: unchecked((int)0x80004001) })
        { return (false, default, "The active debugger engine does not implement this optional query: " + ex.Message); }
    }
}
