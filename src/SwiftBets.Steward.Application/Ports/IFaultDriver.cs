namespace SwiftBets.Steward.Application.Ports;

public interface IFaultDriver
{
    IReadOnlyList<string> Faults { get; }

    Task<string> InjectAsync(string fault, CancellationToken cancellationToken);
}
