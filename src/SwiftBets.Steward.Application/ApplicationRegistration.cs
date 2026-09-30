using Microsoft.Extensions.DependencyInjection;
using SwiftBets.Steward.Application.Agent;
using SwiftBets.Steward.Application.Detection;
using SwiftBets.Steward.Application.Incidents;

namespace SwiftBets.Steward.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddStewardApplication(this IServiceCollection services)
    {
        services.AddScoped<DiagnosisAgent>();
        services.AddScoped<RaiseIncidentHandler>();
        services.AddScoped<DecideActionHandler>();
        services.AddScoped<DetectionRules>();
        return services;
    }
}
