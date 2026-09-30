using SwiftBets.Steward.Application.Model;

namespace SwiftBets.Steward.Application.Ports;

public interface ILanguageModel
{
    Task<ModelTurn> CompleteAsync(ModelRequest request, CancellationToken cancellationToken);
}
