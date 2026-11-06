namespace NetVsMcp.Contracts;

public sealed class TestExplorerRequest
{
    public string? ProjectName { get; set; }
    public string? Filter { get; set; }
    public bool SingleCase { get; set; }
    public string? RunId { get; set; }
}
