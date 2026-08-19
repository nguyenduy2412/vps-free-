using CloudServiceStore.Application.ContactRequests;
using CloudServiceStore.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CloudServiceStore.Integration.Tests;

public sealed class ContactRequestSqlServerMigrationTests
{
    [Fact]
    public async Task SqlServer_migrates_empty_contact_request_test_database()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTACT_TEST_SQLSERVER_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            // Local developers may not have a disposable SQL Server. CI must set this
            // variable to a dedicated ContactRequestIntegration_* database.
            return;
        }

        if (!connectionString.Contains("ContactRequestIntegration_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "CONTACT_TEST_SQLSERVER_CONNECTION_STRING must target a disposable ContactRequestIntegration_* database.");
        }

        var options = new DbContextOptionsBuilder<CloudServiceStoreDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using var context = new CloudServiceStoreDbContext(options);
        await context.Database.EnsureDeletedAsync();
        await context.Database.MigrateAsync();

        Assert.True(await context.Database.CanConnectAsync());
        var tableNames = context.Database.GetDbConnection().GetSchema("Tables")
            .Rows.Cast<System.Data.DataRow>()
            .Select(row => row["TABLE_NAME"]?.ToString())
            .ToArray();
        Assert.Contains("ContactRequests", tableNames);
        Assert.Contains("ContactRequestStatusHistories", tableNames);
    }

    [Fact]
    public async Task SqlServer_concurrent_same_email_allows_only_one_contact_request()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTACT_TEST_SQLSERVER_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        if (!connectionString.Contains("ContactRequestIntegration_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "CONTACT_TEST_SQLSERVER_CONNECTION_STRING must target a disposable ContactRequestIntegration_* database.");
        }

        var options = new DbContextOptionsBuilder<CloudServiceStoreDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await using (var setup = new CloudServiceStoreDbContext(options))
        {
            await setup.Database.EnsureDeletedAsync();
            await setup.Database.MigrateAsync();
        }

        using var gate = new Barrier(2);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => Task.Run(async () =>
        {
            await using var context = new CloudServiceStoreDbContext(options);
            var service = new ContactRequestService(new ContactRequestRepository(context));
            gate.SignalAndWait();

            try
            {
                await service.CreateAsync(
                    new CreateContactRequestRequest(
                        "Nguyen Phuoc Duy",
                        "DUY@EXAMPLE.COM",
                        "0901234567",
                        null,
                        "Tư vấn cloud",
                        "Tôi cần tư vấn dịch vụ cloud cho doanh nghiệp."),
                    null,
                    "127.0.0.1",
                    CancellationToken.None);
                return "created";
            }
            catch (ContactRequestConflictException)
            {
                return "conflict";
            }
        })));

        Assert.Equal(1, outcomes.Count(outcome => outcome == "created"));
        Assert.Equal(1, outcomes.Count(outcome => outcome == "conflict"));

        await using var assertionContext = new CloudServiceStoreDbContext(options);
        Assert.Equal(1, await assertionContext.ContactRequests.CountAsync());
    }
}
