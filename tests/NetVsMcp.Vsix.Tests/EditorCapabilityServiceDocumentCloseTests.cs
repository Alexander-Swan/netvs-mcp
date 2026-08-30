using NetVsMcp.Vsix;

namespace NetVsMcp.Vsix.Tests;

public class EditorCapabilityServiceDocumentCloseTests
{
    [Fact]
    public void CreateClosedDocumentInfo_ReturnsClosedSavedSnapshot()
    {
        var openDocument = new EditorDocumentInfo(
            "Program.cs",
            @"C:\Code\App\Program.cs",
            "CSharp",
            isOpen: true,
            isSaved: false);

        var closedDocument = EditorCapabilityService.CreateClosedDocumentInfo(openDocument);

        Assert.Equal(openDocument.Name, closedDocument.Name);
        Assert.Equal(openDocument.Path, closedDocument.Path);
        Assert.Equal(openDocument.Language, closedDocument.Language);
        Assert.False(closedDocument.IsOpen);
        Assert.True(closedDocument.IsSaved);
    }
}
