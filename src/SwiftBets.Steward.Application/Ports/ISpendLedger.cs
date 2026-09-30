using SwiftBets.Steward.Application.Model;

namespace SwiftBets.Steward.Application.Ports;

public interface ISpendLedger
{
    Task<decimal> MonthToDateUsdAsync(CancellationToken cancellationToken);

    Task RecordAsync(Guid incidentId, string model, ModelUsage usage, decimal costUsd, CancellationToken cancellationToken);
}
