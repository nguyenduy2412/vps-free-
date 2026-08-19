using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CloudServiceStore.WebApi.Security;

public static class ContactRequestRateLimitExtensions
{
    public const string PolicyName = "contact-requests";

    public static IServiceCollection AddContactRequestRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var permitLimit = Math.Max(1, configuration.GetValue("RateLimiting:ContactPermitLimit", 5));
        var window = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("RateLimiting:WindowSeconds", 60)));

        services.AddRateLimiter(options =>
        {
            // This repeats the shared API default explicitly for the Contact
            // policy without replacing the shared OnRejected response body.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(PolicyName, context =>
            {
                var client = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(
                    $"{PolicyName}:{client}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = window,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });
        return services;
    }
}
