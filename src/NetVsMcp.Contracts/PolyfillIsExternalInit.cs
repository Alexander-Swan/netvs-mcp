#if NETSTANDARD2_0
namespace System.Runtime.CompilerServices;

// netstandard2.0 predates C# 9 init-only setters/records; this well-known marker type is normally
// supplied by the runtime on net5.0+ but must be polyfilled to compile records/init accessors here.
// See ARCH-9 in docs/IMPROVEMENT_PLAN.md: this lets NetVsMcp.Contracts multi-target netstandard2.0
// so NetVsMcp.Vsix (net472) can reference it directly instead of hand-duplicating its DTOs.
internal static class IsExternalInit
{
}
#endif
