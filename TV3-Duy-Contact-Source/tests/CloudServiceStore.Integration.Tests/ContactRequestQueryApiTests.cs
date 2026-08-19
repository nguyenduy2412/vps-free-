using System.Net;
using System.Text.Json;
using CloudServiceStore.Domain.Entities;
using CloudServiceStore.Domain.Enums;
using CloudServiceStore.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace CloudServiceStore.Integration.Tests;

public sealed class ContactRequestQueryApiTests(CloudServiceStoreApiFactory factory)
    : IClassFixture<CloudServiceStoreApiFactory>
{
    [Fact]
    public async Task Admin_list_filters_status_and_returns_descending_pages()
    {
        var token = Guid.NewGuid().ToString("N");
        var oldestPending = NewRequest($"Paging {token} oldest", ContactRequestStatus.Pending);
        var middlePending = NewRequest($"Paging {token} middle", ContactRequestStatus.Pending);
        var newestPending = NewRequest($"Paging {token} newest", ContactRequestStatus.Pending);
        var contacted = NewRequest($"Paging {token} contacted", ContactRequestStatus.Contacted);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-10);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CloudServiceStoreDbContext>();
            db.ContactRequests.AddRange(oldestPending, middlePending, newestPending, contacted);
            await db.SaveChangesAsync();

            oldestPending.CreatedAt = baseTime;
            middlePending.CreatedAt = baseTime.AddMinutes(1);
            newestPending.CreatedAt = baseTime.AddMinutes(2);
            contacted.CreatedAt = baseTime.AddMinutes(3);
            await db.SaveChangesAsync();
        }

        using var admin = CreateAuthenticatedClient("Admin");
        var search = Uri.EscapeDataString(token);
        using var firstPageResponse = await admin.GetAsync(
            $"/api/v1/contact-requests?page=1&pageSize=2&search={search}&status={(int)ContactRequestStatus.Pending}");
        using var secondPageResponse = await admin.GetAsync(
            $"/api/v1/contact-requests?page=2&pageSize=2&search={search}&status={(int)ContactRequestStatus.Pending}");
        using var firstPage = JsonDocument.Parse(await firstPageResponse.Content.ReadAsStreamAsync());
        using var secondPage = JsonDocument.Parse(await secondPageResponse.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, firstPageResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondPageResponse.StatusCode);
        Assert.Equal(3, firstPage.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(1, firstPage.RootElement.GetProperty("page").GetInt32());
        Assert.Equal(2, firstPage.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(2, firstPage.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(newestPending.Id, firstPage.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
        Assert.Equal(middlePending.Id, firstPage.RootElement.GetProperty("items")[1].GetProperty("id").GetGuid());
        Assert.All(firstPage.RootElement.GetProperty("items").EnumerateArray(), item =>
            Assert.Equal((int)ContactRequestStatus.Pending, item.GetProperty("status").GetInt32()));

        Assert.Equal(3, secondPage.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, secondPage.RootElement.GetProperty("page").GetInt32());
        Assert.Single(secondPage.RootElement.GetProperty("items").EnumerateArray());
        Assert.Equal(oldestPending.Id, secondPage.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Contact_list_requires_manage_contact_requests_policy()
    {
        using var anonymous = factory.CreateClient();
        using var customer = CreateAuthenticatedClient("Customer");
        using var editor = CreateAuthenticatedClient("Editor");

        using var anonymousResponse = await anonymous.GetAsync("/api/v1/contact-requests?page=1&pageSize=5");
        using var customerResponse = await customer.GetAsync("/api/v1/contact-requests?page=1&pageSize=5");
        using var editorResponse = await editor.GetAsync("/api/v1/contact-requests?page=1&pageSize=5");

        Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, customerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, editorResponse.StatusCode);
    }

    [Fact]
    public async Task Admin_list_filters_created_date_range()
    {
        var token = Guid.NewGuid().ToString("N");
        var before = NewRequest($"Date range {token} before", ContactRequestStatus.Pending);
        var inside = NewRequest($"Date range {token} inside", ContactRequestStatus.Pending);
        var after = NewRequest($"Date range {token} after", ContactRequestStatus.Pending);
        var baseTime = DateTimeOffset.UtcNow.AddMinutes(-20);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CloudServiceStoreDbContext>();
            db.ContactRequests.AddRange(before, inside, after);
            await db.SaveChangesAsync();
            before.CreatedAt = baseTime;
            inside.CreatedAt = baseTime.AddMinutes(5);
            after.CreatedAt = baseTime.AddMinutes(10);
            await db.SaveChangesAsync();
        }

        using var admin = CreateAuthenticatedClient("Admin");
        var search = Uri.EscapeDataString(token);
        var createdFrom = Uri.EscapeDataString(baseTime.AddMinutes(4).ToString("O"));
        var createdTo = Uri.EscapeDataString(baseTime.AddMinutes(6).ToString("O"));
        using var response = await admin.GetAsync(
            $"/api/v1/contact-requests?page=1&pageSize=10&search={search}&createdFrom={createdFrom}&createdTo={createdTo}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, document.RootElement.GetProperty("totalCount").GetInt32());
        Assert.Equal(inside.Id, document.RootElement.GetProperty("items")[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Admin_detail_includes_status_history()
    {
        var request = NewRequest($"Detail history {Guid.NewGuid():N}", ContactRequestStatus.Contacted);
        var history = new ContactRequestStatusHistory
        {
            Id = Guid.NewGuid(),
            ContactRequestId = request.Id,
            FromStatus = ContactRequestStatus.Pending,
            ToStatus = ContactRequestStatus.Contacted,
            Note = "Đã liên hệ.",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-1)
        };

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CloudServiceStoreDbContext>();
            db.ContactRequests.Add(request);
            db.ContactRequestStatusHistories.Add(history);
            await db.SaveChangesAsync();
        }

        using var admin = CreateAuthenticatedClient("Admin");
        using var response = await admin.GetAsync($"/api/v1/contact-requests/{request.Id}");
        using var document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(request.Id, document.RootElement.GetProperty("id").GetGuid());
        Assert.Single(document.RootElement.GetProperty("statusHistory").EnumerateArray());
        Assert.Equal(history.Id, document.RootElement.GetProperty("statusHistory")[0].GetProperty("id").GetGuid());
    }

    private HttpClient CreateAuthenticatedClient(string role)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, role);
        return client;
    }

    private static ContactRequest NewRequest(string fullName, ContactRequestStatus status) => new()
    {
        FullName = fullName,
        Email = $"{Guid.NewGuid():N}@example.test",
        PhoneNumber = "0901234567",
        Subject = "Query test",
        Message = "Payload kỹ thuật dùng để kiểm tra query Contact Request.",
        Status = status
    };
}
