using System.Threading.Channels;
using SwiftBets.Steward.Application.Ports;

namespace SwiftBets.Steward.Infrastructure.Workers;

public sealed class DiagnosisQueue : IDiagnosisQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateBounded<Guid>(new BoundedChannelOptions(1_000) { FullMode = BoundedChannelFullMode.Wait });

    public ChannelReader<Guid> Reader => _channel.Reader;

    public ValueTask EnqueueAsync(Guid incidentId, CancellationToken cancellationToken) => _channel.Writer.WriteAsync(incidentId, cancellationToken);
}
