using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.Contracts.Messaging;
using SwiftBets.Steward.Application.Detection;
using SwiftBets.Steward.Application.Ports;
using SwiftBets.Steward.Infrastructure.Persistence;

namespace SwiftBets.Steward.Infrastructure.Messaging;

/// <summary>
/// Watches every dead-letter queue. Dead-lettered payloads are by definition not valid envelopes, so this reads raw
/// bytes and headers instead of using the typed consumer host (which would dead-letter them again).
/// </summary>
public sealed partial class DeadLetterWatcher(IServiceScopeFactory scopes, IOptions<KafkaOptions> kafka, ILogger<DeadLetterWatcher> logger) : BackgroundService
{
    private static readonly string[] Watched = [Topics.ResultPublished, Topics.CouponPlaced, Topics.LegEvaluated, Topics.CouponSettled];

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Factory.StartNew(() => RunAsync(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

    private async Task RunAsync(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, byte[]>(new ConsumerConfig
        {
            BootstrapServers = kafka.Value.BootstrapServers,
            GroupId = "swiftbets.steward.dead-letters",
            EnableAutoCommit = false,
            AutoOffsetReset = AutoOffsetReset.Latest,
            AllowAutoCreateTopics = false,
        }).Build();
        consumer.Subscribe(Watched.Select(t => TopicName.For(t, kafka.Value.Environment).DeadLetter().Value));
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<string, byte[]>? result;
                try
                {
                    result = consumer.Consume(TimeSpan.FromMilliseconds(500));
                }
                catch (ConsumeException ex) when (!ex.Error.IsFatal)
                {
                    LogConsumeError(ex);
                    continue;
                }

                if (result is null)
                {
                    continue;
                }

                try
                {
                    await RecordAsync(result, stoppingToken);
                    consumer.Commit(result);
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    LogRecordFailed(ex);
                    consumer.Seek(result.TopicPartitionOffset);
                    await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            consumer.Close();
        }
    }

    private async Task RecordAsync(ConsumeResult<string, byte[]> result, CancellationToken cancellationToken)
    {
        string Header(string name) => result.Message.Headers.TryGetLastBytes(name, out var bytes) ? Encoding.UTF8.GetString(bytes) : string.Empty;
        var source = Header(MessageHeaders.DeadLetterSourceTopic);
        var reason = Header(MessageHeaders.DeadLetterReason);
        var payload = JsonSerializer.Serialize(new
        {
            sourceTopic = source,
            sourcePartition = Header(MessageHeaders.DeadLetterSourcePartition),
            sourceOffset = Header(MessageHeaders.DeadLetterSourceOffset),
            reason,
            key = result.Message.Key,
            bodyPreview = Encoding.UTF8.GetString(result.Message.Value.AsSpan(0, Math.Min(result.Message.Value.Length, 200))),
        });

        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<PostgresEventLog>().AppendAsync(
            new RecentEvent(result.Topic, result.Message.Key ?? string.Empty, "dead-letter", null, result.Message.Timestamp.UtcDateTime, payload), source, cancellationToken);
        await scope.ServiceProvider.GetRequiredService<DetectionRules>().OnDeadLetterAsync(source, result.Message.Key ?? string.Empty, reason, cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Dead-letter watcher consume error")]
    private partial void LogConsumeError(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Recording a dead letter failed; retrying")]
    private partial void LogRecordFailed(Exception exception);
}
