using SwiftBets.Steward.Domain.Remediation;

namespace SwiftBets.Steward.Application.Ports;

public interface IRemediationExecutor
{
    /// <summary>Calls the owning service; the action id is the idempotency key, so a retried execution cannot act twice.</summary>
    Task<(bool Succeeded, string Outcome)> ExecuteAsync(RemediationAction action, CancellationToken cancellationToken);
}
