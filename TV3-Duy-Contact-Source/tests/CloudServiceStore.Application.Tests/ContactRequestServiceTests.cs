using CloudServiceStore.Application.ContactRequests;
using CloudServiceStore.Domain.Entities;
using CloudServiceStore.Domain.Enums;
using Moq;

namespace CloudServiceStore.Application.Tests;

public sealed class ContactRequestServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 18, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Create_normalizes_input_persists_initial_event_and_minimal_audit_atomically()
    {
        var repository = CreateRepository();
        var service = CreateService(repository);
        var ownerId = Guid.NewGuid();

        var result = await service.CreateAsync(
            ValidCreateRequest() with
            {
                FullName = "  Nguyen Phuoc Duy  ",
                Email = "DUY@EXAMPLE.COM ",
                PhoneNumber = "0901 234 567"
            },
            ownerId,
            "127.0.0.1",
            CancellationToken.None);

        Assert.Equal(ContactRequestStatus.Pending, result.Status);
        Assert.Equal(Now, result.CreatedAt);
        repository.Verify(x => x.TryCreateAsync(
            It.Is<ContactRequest>(item =>
            item.AppUserId == ownerId
            && item.FullName == "Nguyen Phuoc Duy"
            && item.Email == "duy@example.com"
            && item.PhoneNumber == "0901234567"
            && item.StatusHistory.Count == 1
            && item.StatusHistory.Single().FromStatus == ContactRequestStatus.Pending
            && item.StatusHistory.Single().ToStatus == ContactRequestStatus.Pending),
            It.Is<AuditLog>(audit =>
                audit.AppUserId == ownerId
                && audit.Action == "ContactRequest.Created"
                && audit.OccurredAt == Now
                && audit.CreatedAt == Now
                && audit.NewValuesJson != null
                && audit.NewValuesJson.Contains("\"Status\":1")
                && !audit.NewValuesJson.Contains("duy@example.com")),
            Now.AddHours(-24),
            CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData("not-an-email", "0901234567", "Message body for support")]
    [InlineData("duy@example.com", "123", "Message body for support")]
    [InlineData("duy@example.com", "0901234567", "short")]
    public async Task Create_rejects_invalid_public_input(string email, string phone, string message)
    {
        var service = CreateService(CreateRepository());
        var request = ValidCreateRequest() with { Email = email, PhoneNumber = phone, Message = message };

        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.CreateAsync(request, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Create_rejects_recent_duplicate_email()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.TryCreateAsync(
                It.IsAny<ContactRequest>(),
                It.IsAny<AuditLog>(),
                Now.AddHours(-24),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ContactRequestConflictException>(() =>
            service.CreateAsync(ValidCreateRequest(), null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Create_audit_metadata_does_not_include_full_contact_message()
    {
        var repository = CreateRepository();
        var service = CreateService(repository);
        const string privateMessage = "Nội dung tư vấn nội bộ không được đưa vào audit metadata.";

        await service.CreateAsync(
            ValidCreateRequest() with { Message = privateMessage },
            null,
            "127.0.0.1",
            CancellationToken.None);

        repository.Verify(x => x.TryCreateAsync(
            It.IsAny<ContactRequest>(),
            It.Is<AuditLog>(audit => audit.NewValuesJson != null
                && !audit.NewValuesJson.Contains("duy@example.com")
                && !audit.NewValuesJson.Contains(privateMessage)),
            It.IsAny<DateTimeOffset>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ContactRequestStatus.Rejected)]
    [InlineData(ContactRequestStatus.Cancelled)]
    public async Task Terminal_negative_status_requires_note(ContactRequestStatus nextStatus)
    {
        var repository = CreateRepository();
        var request = ExistingRequest(ContactRequestStatus.Pending);
        repository.Setup(x => x.FindAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.UpdateStatusAsync(request.Id, new(nextStatus, null), Guid.NewGuid(), null, CancellationToken.None));
    }

    [Fact]
    public async Task Contacted_to_approved_adds_history_and_audit()
    {
        var repository = CreateRepository();
        var request = ExistingRequest(ContactRequestStatus.Contacted);
        var actorId = Guid.NewGuid();
        repository.Setup(x => x.FindAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        var service = CreateService(repository);

        var result = await service.UpdateStatusAsync(
            request.Id,
            new(ContactRequestStatus.Approved, "Đã duyệt yêu cầu tư vấn."),
            actorId,
            "127.0.0.1",
            CancellationToken.None);

        Assert.Equal(ContactRequestStatus.Approved, result.Status);
        Assert.Equal(actorId, result.ResolvedBy);
        Assert.Equal(Now, result.ResolvedAt);
        Assert.Single(result.StatusHistory);
        Assert.Contains(result.StatusHistory, history =>
            history.FromStatus == ContactRequestStatus.Contacted
            && history.ToStatus == ContactRequestStatus.Approved
            && history.ChangedBy == actorId);
        repository.Verify(x => x.AddAudit(
            actorId,
            "ContactRequest.StatusChanged",
            nameof(ContactRequest),
            request.Id,
            It.IsAny<string>(),
            It.IsAny<string>(),
            Now,
            "127.0.0.1"), Times.Once);
    }

    [Fact]
    public async Task Status_change_audit_metadata_does_not_include_full_contact_message()
    {
        var repository = CreateRepository();
        const string privateMessage = "Thông tin khách hàng riêng tư không được log nguyên văn.";
        var request = ExistingRequest(ContactRequestStatus.Contacted);
        request.Message = privateMessage;
        var actorId = Guid.NewGuid();
        repository.Setup(x => x.FindAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        var service = CreateService(repository);

        await service.UpdateStatusAsync(
            request.Id,
            new(ContactRequestStatus.Approved, "Đã xử lý."),
            actorId,
            null,
            CancellationToken.None);

        repository.Verify(x => x.AddAudit(
            actorId,
            "ContactRequest.StatusChanged",
            nameof(ContactRequest),
            request.Id,
            It.Is<string?>(oldValues => oldValues != null && !oldValues.Contains(privateMessage)),
            It.Is<string?>(newValues => newValues != null && !newValues.Contains(privateMessage)),
            Now,
            null), Times.Once);
    }

    [Fact]
    public async Task Terminal_status_cannot_be_reopened()
    {
        var repository = CreateRepository();
        var request = ExistingRequest(ContactRequestStatus.Approved);
        repository.Setup(x => x.FindAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ContactRequestConflictException>(() =>
            service.UpdateStatusAsync(
                request.Id,
                new(ContactRequestStatus.Contacted, "Reopen"),
                Guid.NewGuid(),
                null,
                CancellationToken.None));
    }

    [Fact]
    public async Task Pending_cannot_skip_directly_to_approved()
    {
        var repository = CreateRepository();
        var request = ExistingRequest(ContactRequestStatus.Pending);
        repository.Setup(x => x.FindAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ContactRequestConflictException>(() =>
            service.UpdateStatusAsync(request.Id, new(ContactRequestStatus.Approved, "Skip"), Guid.NewGuid(), null, CancellationToken.None));
    }

    [Fact]
    public async Task Get_rejects_invalid_pagination_before_repository_call()
    {
        var repository = CreateRepository();
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.GetAsync(new(Page: 0), CancellationToken.None));

        repository.Verify(x => x.GetAsync(It.IsAny<ContactRequestQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_status_returns_not_found_for_missing_request()
    {
        var repository = CreateRepository();
        repository.Setup(x => x.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactRequest?)null);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ContactRequestNotFoundException>(() =>
            service.UpdateStatusAsync(Guid.NewGuid(), new(ContactRequestStatus.Contacted, "Đã liên hệ."),
                Guid.NewGuid(), null, CancellationToken.None));
    }

    [Fact]
    public async Task Update_status_rejects_same_status()
    {
        var repository = CreateRepository();
        var request = ExistingRequest(ContactRequestStatus.Pending);
        repository.Setup(x => x.FindAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        var service = CreateService(repository);

        await Assert.ThrowsAsync<ContactRequestConflictException>(() =>
            service.UpdateStatusAsync(request.Id, new(ContactRequestStatus.Pending, "No change"),
                Guid.NewGuid(), null, CancellationToken.None));
    }

    [Fact]
    public async Task Get_maps_paged_items_from_repository()
    {
        var repository = CreateRepository();
        var request = ExistingRequest(ContactRequestStatus.Pending);
        repository.Setup(x => x.GetAsync(It.IsAny<ContactRequestQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(((IReadOnlyList<ContactRequest>)[request], 1));
        var service = CreateService(repository);

        var result = await service.GetAsync(new(Page: 1, PageSize: 20), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(request.Id, result.Items[0].Id);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Get_by_id_maps_ordered_history_and_returns_null_for_empty_id()
    {
        var repository = CreateRepository();
        var request = ExistingRequest(ContactRequestStatus.Contacted);
        var firstHistory = new ContactRequestStatusHistory
        {
            Id = Guid.NewGuid(),
            ContactRequestId = request.Id,
            FromStatus = ContactRequestStatus.Pending,
            ToStatus = ContactRequestStatus.Contacted,
            Note = "Đã liên hệ.",
            ChangedBy = Guid.NewGuid(),
            CreatedAt = Now.AddMinutes(2)
        };
        var receivedHistory = new ContactRequestStatusHistory
        {
            Id = Guid.NewGuid(),
            ContactRequestId = request.Id,
            FromStatus = ContactRequestStatus.Pending,
            ToStatus = ContactRequestStatus.Pending,
            Note = "Đã nhận yêu cầu.",
            ChangedBy = null,
            CreatedAt = Now.AddMinutes(1)
        };
        request.StatusHistory.Add(firstHistory);
        request.StatusHistory.Add(receivedHistory);
        repository.Setup(x => x.FindAsync(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(request);
        var service = CreateService(repository);

        var detail = await service.GetByIdAsync(request.Id, CancellationToken.None);
        var empty = await service.GetByIdAsync(Guid.Empty, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.Equal(request.Message, detail.Message);
        Assert.Equal(ContactRequestStatus.Contacted, detail.Status);
        Assert.Equal([receivedHistory.Id, firstHistory.Id], detail.StatusHistory.Select(item => item.Id));
        Assert.Equal(
            [ContactRequestStatus.Approved, ContactRequestStatus.Rejected, ContactRequestStatus.Cancelled],
            detail.AllowedTransitions);
        Assert.Null(empty);
        repository.Verify(x => x.FindAsync(Guid.Empty, It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Update_status_rejects_invalid_id_actor_enum_and_note_length_before_repository_call()
    {
        var repository = CreateRepository();
        var service = CreateService(repository);
        var actorId = Guid.NewGuid();

        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.UpdateStatusAsync(Guid.Empty, new(ContactRequestStatus.Contacted, "Đã liên hệ."), actorId, null, CancellationToken.None));
        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.UpdateStatusAsync(Guid.NewGuid(), new(ContactRequestStatus.Contacted, "Đã liên hệ."), Guid.Empty, null, CancellationToken.None));
        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.UpdateStatusAsync(Guid.NewGuid(), new((ContactRequestStatus)999, "Invalid"), actorId, null, CancellationToken.None));
        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.UpdateStatusAsync(Guid.NewGuid(), new(ContactRequestStatus.Contacted, new string('x', 1001)), actorId, null, CancellationToken.None));

        repository.Verify(x => x.FindAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_rejects_invalid_name_company_and_subject_boundaries()
    {
        var service = CreateService(CreateRepository());

        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.CreateAsync(ValidCreateRequest() with { FullName = "X" }, null, null, CancellationToken.None));
        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.CreateAsync(ValidCreateRequest() with { CompanyName = new string('C', 161) }, null, null, CancellationToken.None));
        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.CreateAsync(ValidCreateRequest() with { Subject = "X" }, null, null, CancellationToken.None));
    }

    [Fact]
    public async Task Get_rejects_long_search_invalid_status_and_inverted_date_range()
    {
        var service = CreateService(CreateRepository());

        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.GetAsync(new(Search: new string('s', 257)), CancellationToken.None));
        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.GetAsync(new(Status: (ContactRequestStatus)999), CancellationToken.None));
        await Assert.ThrowsAsync<ContactRequestValidationException>(() =>
            service.GetAsync(new(CreatedFrom: Now, CreatedTo: Now.AddMinutes(-1)), CancellationToken.None));
    }

    private static ContactRequestService CreateService(Mock<IContactRequestRepository> repository) =>
        new(repository.Object, new FixedTimeProvider(Now));

    private static Mock<IContactRequestRepository> CreateRepository()
    {
        var repository = new Mock<IContactRequestRepository>();
        repository.Setup(x => x.TryCreateAsync(
                It.IsAny<ContactRequest>(),
                It.IsAny<AuditLog>(),
                It.IsAny<DateTimeOffset>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repository;
    }

    private static CreateContactRequestRequest ValidCreateRequest() =>
        new(
            "Nguyen Phuoc Duy",
            "duy@example.com",
            "0901234567",
            "Cloud Service Store",
            "Tư vấn gói cloud",
            "Tôi cần tư vấn gói cloud cho doanh nghiệp.");

    private static ContactRequest ExistingRequest(ContactRequestStatus status) =>
        new()
        {
            Id = Guid.NewGuid(),
            FullName = "Nguyen Phuoc Duy",
            Email = "duy@example.com",
            PhoneNumber = "0901234567",
            Subject = "Tư vấn",
            Message = "Tôi cần tư vấn gói cloud cho doanh nghiệp.",
            Status = status,
            CreatedAt = Now.AddDays(-1)
        };

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
