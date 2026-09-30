namespace SwiftBets.Steward.Application.Model;

public sealed record ModelUsage(long InputTokens, long OutputTokens, long CacheReadTokens, long CacheWriteTokens);
