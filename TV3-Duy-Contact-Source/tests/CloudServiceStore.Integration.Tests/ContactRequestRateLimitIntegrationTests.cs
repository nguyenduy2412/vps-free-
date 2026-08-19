using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CloudServiceStore.Integration.Tests;

public sealed class ContactRequestRateLimitIntegrationTests(CloudServiceStoreApiFactory factory)
    : IClassFixture<CloudServiceStoreApiFactory>
{
    [Fact]
    public async Task Public_contact_request_returns_429_after_contact_permit_limit()
    {
        using var client = factory.CreateClient();

        for (var index = 0; index < 2; index++)
        {
            using var accepted = await client.PostAsJsonAsync(
                "/api/v1/contact-requests",
                RequestFor(index));

            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        }

        using var rejected = await client.PostAsJsonAsync(
            "/api/v1/contact-requests",
            RequestFor(2));

        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        var problem = await rejected.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status429TooManyRequests, problem.Status);
    }

    private static object RequestFor(int index) => new
    {
        fullName = "Integration Contact",
        email = $"contact-rate-limit-{index}-{Guid.NewGuid():N}@example.test",
        phoneNumber = "0901234567",
        companyName = "CloudServiceStore Test",
        subject = "Rate limit integration test",
        message = "This request validates that the third public submit receives HTTP 429."
    };
}
