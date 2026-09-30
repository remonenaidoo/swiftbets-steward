using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using SwiftBets.Steward.Infrastructure.Persistence;
using SwiftBets.Steward.Infrastructure.Runbooks;

namespace SwiftBets.Steward.Infrastructure.Workers;

/// <summary>Ingests runbooks on start (re-embedding only changed sections) and keeps the event log to a rolling window.</summary>
public sealed partial class HousekeepingWorker(PgvectorRunbookStore runbooks, PostgresEventLog events, TimeProvider time, ILogger<HousekeepingWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Retention = TimeSpan.FromHours(3);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var embedded = await runbooks.IngestAsync(RunbookLibrary.Load(), stoppingToken);
                LogIngested(embedded);
                break;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogIngestFailed(ex);
                await Task.Delay(TimeSpan.FromSeconds(10), time, stoppingToken);
            }
        }

        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(10), time);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await events.PruneAsync(time.GetUtcNow() - Retention, stoppingToken);
            }
            catch (NpgsqlException ex)
            {
                LogPruneFailed(ex);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Runbook ingestion failed; retrying")]
    private partial void LogIngestFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Event log pruning failed")]
    private partial void LogPruneFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Runbooks ingested; {Embedded} sections (re)embedded")]
    private partial void LogIngested(int embedded);
}
