using System.Linq;
using Microsoft.CodeAnalysis;

namespace NetVsMcp.Vsix;

internal static class SourceOnlyChangePolicy
{
    public static bool Accepts(Solution before, Solution after)
    {
        var changes = after.GetChanges(before);
        if (changes.GetAddedProjects().Any() || changes.GetRemovedProjects().Any()) return false;
        foreach (var project in changes.GetProjectChanges())
        {
            if (project.GetAddedDocuments().Any() || project.GetRemovedDocuments().Any() ||
                project.GetAddedAdditionalDocuments().Any() || project.GetRemovedAdditionalDocuments().Any() || project.GetChangedAdditionalDocuments().Any() ||
                project.GetAddedAnalyzerConfigDocuments().Any() || project.GetRemovedAnalyzerConfigDocuments().Any() || project.GetChangedAnalyzerConfigDocuments().Any() ||
                project.GetAddedProjectReferences().Any() || project.GetRemovedProjectReferences().Any() ||
                project.GetAddedMetadataReferences().Any() || project.GetRemovedMetadataReferences().Any() ||
                project.GetAddedAnalyzerReferences().Any() || project.GetRemovedAnalyzerReferences().Any()) return false;
            var oldProject = project.OldProject;
            var newProject = project.NewProject;
            if (oldProject.Name != newProject.Name || oldProject.AssemblyName != newProject.AssemblyName ||
                oldProject.FilePath != newProject.FilePath || oldProject.OutputFilePath != newProject.OutputFilePath ||
                !Equals(oldProject.CompilationOptions, newProject.CompilationOptions) || !Equals(oldProject.ParseOptions, newProject.ParseOptions)) return false;
            foreach (var id in newProject.DocumentIds)
            {
                var oldDocument = oldProject.GetDocument(id)!;
                var newDocument = newProject.GetDocument(id)!;
                if (oldDocument.Name != newDocument.Name || oldDocument.FilePath != newDocument.FilePath ||
                    oldDocument.SourceCodeKind != newDocument.SourceCodeKind || !oldDocument.Folders.SequenceEqual(newDocument.Folders)) return false;
            }
        }
        return true;
    }
}
