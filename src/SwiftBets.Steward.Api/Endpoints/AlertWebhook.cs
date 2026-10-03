using System.Security.Cryptography;
using System.Text;
using SwiftBets.Steward.Application.Detection;

namespace SwiftBets.Steward.Api.Endpoints;

/// <summary>
/// Alertmanager's webhook. Each firing alert opens (or joins) the incident its rule names; resolved alerts change nothing,
/// because an incident closes when a person decides it is done. Authenticated by a shared bearer token, off when none is set.
/// </summary>
public static class AlertWebhook
{
    public static IEndpointRouteBuilder MapAlertWebhook(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/alerts/alertmanager", async (AlertmanagerPayload payload, HttpContext context, DetectionRules rules, IConfiguration configuration, CancellationToken cancellationToken) =>
        {
            if (configuration["Steward:AlertWebhookToken"] is not { Length: > 0 } expected)
            {
                return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            }

            var presented = context.Request.Headers.Authorization.ToString();
            if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(presented), Encoding.UTF8.GetBytes($"Bearer {expected}")))
            {
                return Results.Unauthorized();
            }

            var opened = 0;
            var unknown = new List<string>();
            foreach (var alert in payload.Alerts ?? [])
            {
                var labels = alert.Labels ?? new Dictionary<string, string>();
                if (alert.Status != "firing" || !labels.TryGetValue("alertname", out var name))
                {
                    continue;
                }

                var summary = alert.Annotations?.GetValueOrDefault("summary") ?? alert.Annotations?.GetValueOrDefault("description") ?? name;
                if (await rules.OnAlertAsync(name, labels, summary, cancellationToken))
                {
                    opened++;
                }
                else
                {
                    unknown.Add(name);
                }
            }

            return Results.Ok(new { opened, unknown });
        }).AllowAnonymous();
        return endpoints;
    }

    public sealed record AlertmanagerPayload(string? Status, IReadOnlyList<AlertmanagerAlert>? Alerts);

    public sealed record AlertmanagerAlert(string Status, Dictionary<string, string>? Labels, Dictionary<string, string>? Annotations);
}
