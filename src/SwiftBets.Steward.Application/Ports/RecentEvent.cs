namespace SwiftBets.Steward.Application.Ports;

public sealed record RecentEvent(string Topic, string Key, string EventType, Guid? EventId, DateTimeOffset OccurredAt, string PayloadJson);
