namespace SwiftBets.Steward.Domain.Runbooks;

public sealed record RunbookSection(string RunbookId, string Heading, string Text)
{
    public string Key => $"{RunbookId}#{Heading}";
}
