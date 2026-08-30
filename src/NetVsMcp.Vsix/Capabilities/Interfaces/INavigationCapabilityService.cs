using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EnvDTE;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.VisualStudio.ComponentModelHost;
using Microsoft.VisualStudio.LanguageServices;
using Microsoft.VisualStudio.Shell;
using Task = System.Threading.Tasks.Task;

namespace NetVsMcp.Vsix;

internal interface INavigationCapabilityService
{
    Task<GoToDefinitionResult> GoToDefinitionAsync(string documentPath, int line, int column, CancellationToken cancellationToken);
    Task<FindReferencesResult> FindReferencesAsync(string documentPath, int line, int column, CancellationToken cancellationToken);
    Task<FindImplementationsResult> FindImplementationsAsync(string documentPath, int line, int column, CancellationToken cancellationToken);
    Task<CodeWorkspaceSymbolsResult> WorkspaceSymbolsAsync(CodeWorkspaceSymbolsRequest request, CancellationToken cancellationToken);
    Task<CallHierarchyResult> CallHierarchyAsync(CallHierarchyRequest request, CancellationToken cancellationToken);
    Task<RenameSymbolPreviewResult> RenameSymbolPreviewAsync(RenameSymbolRequest request, CancellationToken cancellationToken);
    Task<RenameSymbolApplyResult> RenameSymbolApplyAsync(RenameSymbolRequest request, CancellationToken cancellationToken);
    Task<DocumentSymbolsResult> ListDocumentSymbolsAsync(string? documentPath, CancellationToken cancellationToken);
}
