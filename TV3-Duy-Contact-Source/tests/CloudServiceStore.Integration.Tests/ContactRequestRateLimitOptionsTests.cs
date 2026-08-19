using CloudServiceStore.WebApi.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CloudServiceStore.Integration.Tests;

public sealed class ContactRequestRateLimitOptionsTests
{
    [Fact]
    public void Contact_policy_preserves_shared_rejection_configuration()
    {
        var services = new ServiceCollection();
        Func<OnRejectedContext, CancellationToken, ValueTask> sharedOnRejected =
            (_, _) => ValueTask.CompletedTask;

        services.Configure<RateLimiterOptions>(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = sharedOnRejected;
        });

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RateLimiting:ContactPermitLimit"] = "2",
                ["RateLimiting:WindowSeconds"] = "60"
            })
            .Build();

        services.ConfigureContactRequestRateLimiting(configuration);
        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RateLimiterOptions>>().Value;

        Assert.Equal(StatusCodes.Status429TooManyRequests, options.RejectionStatusCode);
        Assert.Same(sharedOnRejected, options.OnRejected);
    }
}
