using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SwiftBets.Steward.Application.Agent;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Domain.Incidents;

namespace SwiftBets.Steward.Infrastructure.Workers;

/// <summary>Diagnoses incidents one at a time, off the consumer threads. On start it picks up incidents left open by a restart.</summary>
public sealed partial class DiagnosisWorker(DiagnosisQueue queue, IServiceScopeFactory scopes, ILogger<DiagnosisWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using (var scope = scopes.CreateAsyncScope())
        {
            foreach (var open in (await scope.ServiceProvider.GetRequiredService<IIncidentStore>().ListAsync(200, stoppingToken)).Where(i => i.Status is IncidentStatus.Open or IncidentStatus.Diagnosing))
            {
                await queue.EnqueueAsync(open.IncidentId, stoppingToken);
            }
        }

        await foreach (var incidentId in queue.Reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<DiagnosisAgent>().DiagnoseAsync(incidentId, stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                LogFailed(ex, incidentId);
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IIncidentStore>().SaveReportAsync(incidentId, null, [$"diagnosis crashed: {ex.Message}"], [], IncidentStatus.DiagnosisFailed, CancellationToken.None);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Diagnosis of incident {IncidentId} failed")]
    private partial void LogFailed(Exception exception, Guid incidentId);
}
