using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace CloudServiceStore.WebApi.Security;

public static class ContactRequestRateLimitExtensions
{
    public const string PolicyName = "contact-requests";

    public static IServiceCollection ConfigureContactRequestRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var permitLimit = Math.Max(1, configuration.GetValue("RateLimiting:ContactPermitLimit", 5));
        var window = TimeSpan.FromSeconds(Math.Max(1, configuration.GetValue("RateLimiting:WindowSeconds", 60)));

        services.Configure<RateLimiterOptions>(options =>
        {
            // The baseline owns AddRateLimiter, GlobalLimiter, OnRejected and the
            // 429 ProblemDetails response. Contact only contributes one policy.
            options.AddPolicy(PolicyName, context =>
            {
                // Forwarded headers must be applied by the trusted-proxy baseline
                // before rate limiting; never trust a raw client-supplied header.
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
