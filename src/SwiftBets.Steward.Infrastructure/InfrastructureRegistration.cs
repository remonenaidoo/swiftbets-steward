using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SwiftBets.BuildingBlocks.Core;
using SwiftBets.BuildingBlocks.Messaging;
using SwiftBets.BuildingBlocks.Persistence;

namespace SwiftBets.Steward.Infrastructure;

public static class InfrastructureRegistration
{
    public static IServiceCollection AddStewardInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddPostgresPersistence(Required(configuration, "ConnectionStrings:SbSteward"));
        services.AddKafkaMessaging(configuration);
        services.AddFaultInjection(configuration);
        return services;
    }

    private static string Required(IConfiguration configuration, string key) =>
        configuration[key] is { Length: > 0 } value ? value : throw new InvalidOperationException($"Configuration '{key}' is required.");
}
