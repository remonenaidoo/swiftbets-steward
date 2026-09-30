using SwiftBets.Steward.Application.Model;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Model;

/// <summary>No model configured: detection still runs and incidents still open, but diagnosis says why it did not happen.</summary>
public sealed class DisabledLanguageModel : ILanguageModel
{
    public const string StopReason = "model_not_configured";

    public Task<ModelTurn> CompleteAsync(ModelRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new ModelTurn("none", [], StopReason, new ModelUsage(0, 0, 0, 0)));
}
