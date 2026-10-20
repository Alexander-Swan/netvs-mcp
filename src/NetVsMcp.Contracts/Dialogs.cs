namespace NetVsMcp.Contracts;

public sealed record DialogButtonInfo(string ButtonId, string Name, bool IsEnabled);

public sealed record DialogInfo(string DialogId, string Title, string Message, IReadOnlyList<DialogButtonInfo> Buttons, bool Truncated = false);
