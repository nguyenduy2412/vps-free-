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
}
