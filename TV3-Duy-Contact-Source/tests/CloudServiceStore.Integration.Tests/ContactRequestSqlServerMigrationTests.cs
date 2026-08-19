using CloudServiceStore.Application.ContactRequests;
using CloudServiceStore.Infrastructure.Persistence;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Data.SqlClient;

namespace CloudServiceStore.Integration.Tests;

public sealed class ContactRequestSqlServerMigrationTests
{
    [Fact]
    public async Task SqlServer_migrates_empty_contact_request_test_database()
    {
        var connectionString = GetDisposableConnectionStringOrSkip();
        if (connectionString is null) return;

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
        var connectionString = GetDisposableConnectionStringOrSkip();
        if (connectionString is null) return;

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

    [Fact]
    public async Task SqlServer_concurrent_different_emails_do_not_block_each_other()
    {
        var connectionString = GetDisposableConnectionStringOrSkip();
        if (connectionString is null) return;

        var options = new DbContextOptionsBuilder<CloudServiceStoreDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await ResetDatabaseAsync(options);

        using var gate = new Barrier(2);
        var outcomes = await Task.WhenAll(new[] { "duy.one@example.com", "duy.two@example.com" }
            .Select(email => Task.Run(async () =>
            {
                await using var context = new CloudServiceStoreDbContext(options);
                var service = new ContactRequestService(new ContactRequestRepository(context));
                gate.SignalAndWait();

                await service.CreateAsync(CreateRequest(email), null, "127.0.0.1", CancellationToken.None);
                return "created";
            })));

        Assert.All(outcomes, outcome => Assert.Equal("created", outcome));
        await using var assertionContext = new CloudServiceStoreDbContext(options);
        Assert.Equal(2, await assertionContext.ContactRequests.CountAsync());
    }

    [Fact]
    public async Task SqlServer_app_lock_wait_honors_cancellation_and_does_not_persist_request()
    {
        var connectionString = GetDisposableConnectionStringOrSkip();
        if (connectionString is null) return;

        var options = new DbContextOptionsBuilder<CloudServiceStoreDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await ResetDatabaseAsync(options);

        const string email = "locked@example.com";
        await using var lockConnection = new SqlConnection(connectionString);
        await lockConnection.OpenAsync();
        await AcquireSessionLockAsync(lockConnection, EmailLockResource(email));

        await using (var context = new CloudServiceStoreDbContext(options))
        {
            var service = new ContactRequestService(new ContactRequestRepository(context));
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(250));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                service.CreateAsync(CreateRequest(email), null, "127.0.0.1", cancellation.Token));
        }

        await using var assertionContext = new CloudServiceStoreDbContext(options);
        Assert.Equal(0, await assertionContext.ContactRequests.CountAsync());
    }

    [Fact]
    public async Task SqlServer_app_lock_timeout_returns_unavailable_and_does_not_persist_request()
    {
        var connectionString = GetDisposableConnectionStringOrSkip();
        if (connectionString is null) return;

        var options = new DbContextOptionsBuilder<CloudServiceStoreDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        await ResetDatabaseAsync(options);

        const string email = "timeout@example.com";
        await using var lockConnection = new SqlConnection(connectionString);
        await lockConnection.OpenAsync();
        await AcquireSessionLockAsync(lockConnection, EmailLockResource(email));

        await using (var context = new CloudServiceStoreDbContext(options))
        {
            var service = new ContactRequestService(new ContactRequestRepository(context));

            await Assert.ThrowsAsync<ContactRequestLockUnavailableException>(() =>
                service.CreateAsync(CreateRequest(email), null, "127.0.0.1", CancellationToken.None));
        }

        await using var assertionContext = new CloudServiceStoreDbContext(options);
        Assert.Equal(0, await assertionContext.ContactRequests.CountAsync());
    }

    [Fact]
    public async Task SqlServer_rolls_back_contact_request_when_save_changes_fails()
    {
        var connectionString = GetDisposableConnectionStringOrSkip();
        if (connectionString is null) return;

        var baseOptions = new DbContextOptionsBuilder<CloudServiceStoreDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        await ResetDatabaseAsync(baseOptions);

        var failingOptions = new DbContextOptionsBuilder<CloudServiceStoreDbContext>()
            .UseSqlServer(connectionString)
            .AddInterceptors(new ThrowOnContactInsertInterceptor())
            .Options;

        await using (var context = new CloudServiceStoreDbContext(failingOptions))
        {
            var service = new ContactRequestService(new ContactRequestRepository(context));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.CreateAsync(CreateRequest("rollback@example.com"), null, "127.0.0.1", CancellationToken.None));
        }

        await using var assertionContext = new CloudServiceStoreDbContext(baseOptions);
        Assert.Equal(0, await assertionContext.ContactRequests.CountAsync());
    }

    private static async Task ResetDatabaseAsync(DbContextOptions<CloudServiceStoreDbContext> options)
    {
        await using var setup = new CloudServiceStoreDbContext(options);
        await setup.Database.EnsureDeletedAsync();
        await setup.Database.MigrateAsync();
    }

    private static CreateContactRequestRequest CreateRequest(string email) =>
        new(
            "Nguyen Phuoc Duy",
            email,
            "0901234567",
            null,
            "Tư vấn cloud",
            "Tôi cần tư vấn dịch vụ cloud cho doanh nghiệp.");

    private static string EmailLockResource(string normalizedEmail) =>
        $"ContactRequest:Email:{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedEmail)))}";

    private static async Task AcquireSessionLockAsync(SqlConnection connection, string resource)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @result int;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Session',
                @LockTimeout = 0;
            SELECT @result;
            """;
        command.Parameters.Add(new SqlParameter("@resource", resource));

        Assert.True(Convert.ToInt32(await command.ExecuteScalarAsync()) >= 0);
    }

    private sealed class ThrowOnContactInsertInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("INSERT INTO [ContactRequests]", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Simulated contact insert failure.");

            return ValueTask.FromResult(result);
        }
    }

    private static string? GetDisposableConnectionStringOrSkip()
    {
        var connectionString = Environment.GetEnvironmentVariable("CONTACT_TEST_SQLSERVER_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            if (string.Equals(
                    Environment.GetEnvironmentVariable("REQUIRE_CONTACT_SQLSERVER_TESTS"),
                    "true",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "CONTACT_TEST_SQLSERVER_CONNECTION_STRING is required when REQUIRE_CONTACT_SQLSERVER_TESTS=true.");
            }

            return null;
        }

        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog;
        if (string.IsNullOrWhiteSpace(databaseName) ||
            !databaseName.StartsWith("ContactRequestIntegration_", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "CONTACT_TEST_SQLSERVER_CONNECTION_STRING must use an Initial Catalog beginning with ContactRequestIntegration_.");
        }

        return connectionString;
    }
}
