namespace SwiftBets.Steward.Domain.Incidents;

/// <summary>
/// One detected problem. The fingerprint (kind plus subject) de-duplicates repeated signals, so a burst of dead
/// letters or retry schedules raises one incident rather than hundreds.
/// </summary>
public sealed record Incident(Guid IncidentId, IncidentKind Kind, string Subject, string Summary, IncidentStatus Status, DateTimeOffset OpenedAt)
{
    public string Fingerprint => $"{Kind}:{Subject}";

    public static Incident Open(IncidentKind kind, string subject, string summary, DateTimeOffset now) =>
        new(Guid.CreateVersion7(now), kind, subject, summary, IncidentStatus.Open, now);
}
