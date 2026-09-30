using Microsoft.Extensions.DependencyInjection;

namespace SwiftBets.Steward.Application;

public static class ApplicationRegistration
{
    public static IServiceCollection AddStewardApplication(this IServiceCollection services) => services;
}
