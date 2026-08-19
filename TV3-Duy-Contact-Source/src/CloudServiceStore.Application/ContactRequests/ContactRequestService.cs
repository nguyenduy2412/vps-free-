using System.Net.Mail;
using System.Text.Json;
using System.Text.RegularExpressions;
using CloudServiceStore.Application.Common;
using CloudServiceStore.Domain.Entities;
using CloudServiceStore.Domain.Enums;

namespace CloudServiceStore.Application.ContactRequests;

public sealed partial class ContactRequestService(
    IContactRequestRepository repository,
    TimeProvider? timeProvider = null) : IContactRequestService
{
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;

    public async Task<ContactRequestConfirmationDto> CreateAsync(
        CreateContactRequestRequest request,
        Guid? ownerId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        ValidateCreate(request);

        var now = clock.GetUtcNow();
        var email = request.Email.Trim().ToLowerInvariant();

        var contactRequest = new ContactRequest
        {
            AppUserId = ownerId,
            CreatedBy = ownerId,
            FullName = request.FullName.Trim(),
            Email = email,
            PhoneNumber = NormalizePhone(request.PhoneNumber),
            CompanyName = CleanOptional(request.CompanyName),
            Subject = request.Subject.Trim(),
            Message = request.Message.Trim(),
            Status = ContactRequestStatus.Pending,
            CreatedAt = now
        };

        // The initial receipt is an explicit timeline event, not a status transition.
        var history = new ContactRequestStatusHistory
        {
            ContactRequestId = contactRequest.Id,
            FromStatus = ContactRequestStatus.Pending,
            ToStatus = ContactRequestStatus.Pending,
            Note = "Contact request received.",
            ChangedBy = ownerId,
            CreatedBy = ownerId,
            CreatedAt = now
        };

        contactRequest.StatusHistory.Add(history);
        var audit = new AuditLog
        {
            AppUserId = ownerId,
            Action = "ContactRequest.Created",
            EntityName = nameof(ContactRequest),
            EntityId = contactRequest.Id,
            NewValuesJson = JsonSerializer.Serialize(new { contactRequest.Status }),
            IpAddress = ipAddress,
            OccurredAt = now,
            CreatedAt = now,
            CreatedBy = ownerId
        };

        if (!await repository.TryCreateAsync(
                contactRequest,
                audit,
                now.AddHours(-24),
                cancellationToken))
        {
            throw new ContactRequestConflictException(
                "A contact request with this email was submitted recently.");
        }

        return new(contactRequest.Id, contactRequest.Status, contactRequest.CreatedAt);
    }

    public async Task<PagedResult<ContactRequestListItemDto>> GetAsync(
        ContactRequestQuery query,
        CancellationToken cancellationToken)
    {
        ValidateQuery(query);
        var result = await repository.GetAsync(query, cancellationToken);
        return new(
            result.Items.Select(MapList).ToArray(),
            query.Page,
            query.PageSize,
            result.Total);
    }

    public async Task<ContactRequestDetailDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
            return null;

        return (await repository.FindAsync(id, cancellationToken)) is { } item
            ? MapDetail(item)
            : null;
    }

    public async Task<ContactRequestDetailDto> UpdateStatusAsync(
        Guid id,
        UpdateContactRequestStatusRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (id == Guid.Empty || actorId == Guid.Empty)
            throw new ContactRequestValidationException("A valid request and actor are required.");

        if (!Enum.IsDefined(request.Status))
            throw new ContactRequestValidationException(
                "Contact request status is not supported.");

        if (request.Note?.Trim().Length > 1000)
            throw new ContactRequestValidationException(
                "Status note must be 1000 characters or fewer.");

        if (request.Status is ContactRequestStatus.Rejected or ContactRequestStatus.Cancelled
            && string.IsNullOrWhiteSpace(request.Note))
        {
            throw new ContactRequestValidationException(
                "A note is required when rejecting or cancelling a contact request.");
        }

        var contactRequest = await repository.FindAsync(id, cancellationToken)
            ?? throw new ContactRequestNotFoundException(
                "Contact request was not found.");

        if (contactRequest.Status == request.Status)
            throw new ContactRequestConflictException(
                "Contact request already has the selected status.");

        if (!CanTransition(contactRequest.Status, request.Status))
        {
            throw new ContactRequestConflictException(
                $"Cannot change contact request status from {contactRequest.Status} to {request.Status}.");
        }

        var previousStatus = contactRequest.Status;
        var now = clock.GetUtcNow();
        var note = CleanOptional(request.Note);
        var isTerminal = request.Status is ContactRequestStatus.Approved
            or ContactRequestStatus.Rejected
            or ContactRequestStatus.Cancelled;

        contactRequest.Status = request.Status;
        contactRequest.ResolutionNote = note;
        contactRequest.ResolvedBy = isTerminal ? actorId : null;
        contactRequest.ResolvedAt = isTerminal ? now : null;
        contactRequest.UpdatedBy = actorId;
        contactRequest.UpdatedAt = now;

        var history = new ContactRequestStatusHistory
        {
            ContactRequestId = contactRequest.Id,
            FromStatus = previousStatus,
            ToStatus = request.Status,
            Note = note,
            ChangedBy = actorId,
            CreatedBy = actorId,
            CreatedAt = now
        };

        contactRequest.StatusHistory.Add(history);
        repository.AddAudit(
            actorId,
            "ContactRequest.StatusChanged",
            nameof(ContactRequest),
            contactRequest.Id,
            JsonSerializer.Serialize(new { Status = previousStatus }),
            JsonSerializer.Serialize(new
            {
                Status = contactRequest.Status,
                Note = note
            }),
            now,
            ipAddress);

        await repository.SaveChangesAsync(cancellationToken);
        return MapDetail(contactRequest);
    }

    private static bool CanTransition(
        ContactRequestStatus from,
        ContactRequestStatus to) =>
        from switch
        {
            ContactRequestStatus.Pending => to is ContactRequestStatus.Contacted
                or ContactRequestStatus.Rejected
                or ContactRequestStatus.Cancelled,
            ContactRequestStatus.Contacted => to is ContactRequestStatus.Approved
                or ContactRequestStatus.Rejected
                or ContactRequestStatus.Cancelled,
            _ => false
        };

    private static IReadOnlyList<ContactRequestStatus> GetAllowedTransitions(
        ContactRequestStatus currentStatus) =>
        Enum.GetValues<ContactRequestStatus>()
            .Where(candidate => CanTransition(currentStatus, candidate))
            .ToArray();

    private static void ValidateCreate(CreateContactRequestRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.FullName)
            || request.FullName.Trim().Length is < 2 or > 160)
        {
            throw new ContactRequestValidationException(
                "Full name is required and must be from 2 to 160 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Email)
            || request.Email.Trim().Length > 256
            || !IsValidEmail(request.Email))
        {
            throw new ContactRequestValidationException(
                "A valid email address is required.");
        }

        if (string.IsNullOrWhiteSpace(request.PhoneNumber)
            || !PhonePattern().IsMatch(request.PhoneNumber.Trim()))
        {
            throw new ContactRequestValidationException(
                "A valid phone number from 8 to 15 digits is required.");
        }

        if (request.CompanyName?.Trim().Length > 160)
            throw new ContactRequestValidationException(
                "Company name must be 160 characters or fewer.");

        if (string.IsNullOrWhiteSpace(request.Subject)
            || request.Subject.Trim().Length is < 3 or > 180)
        {
            throw new ContactRequestValidationException(
                "Subject is required and must be from 3 to 180 characters.");
        }

        if (string.IsNullOrWhiteSpace(request.Message)
            || request.Message.Trim().Length is < 10 or > 4000)
        {
            throw new ContactRequestValidationException(
                "Message is required and must be from 10 to 4000 characters.");
        }
    }

    private static void ValidateQuery(ContactRequestQuery query)
    {
        if (query.Page < 1 || query.PageSize is < 1 or > 100)
            throw new ContactRequestValidationException(
                "Page must be at least 1 and PageSize must be from 1 to 100.");

        if (query.Search?.Trim().Length > 256)
            throw new ContactRequestValidationException(
                "Search must be 256 characters or fewer.");

        if (query.Status is not null && !Enum.IsDefined(query.Status.Value))
            throw new ContactRequestValidationException(
                "Contact request status is not supported.");

        if (query.CreatedFrom is not null
            && query.CreatedTo is not null
            && query.CreatedTo < query.CreatedFrom)
        {
            throw new ContactRequestValidationException(
                "CreatedTo must be on or after CreatedFrom.");
        }
    }

    private static bool IsValidEmail(string value)
    {
        try
        {
            var trimmed = value.Trim();
            return new MailAddress(trimmed).Address == trimmed;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string NormalizePhone(string value) =>
        Regex.Replace(value.Trim(), "[\\s().-]", string.Empty);

    private static string? CleanOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    [GeneratedRegex(@"^\+?(?:[\d][\s().-]*){8,15}$")]
    private static partial Regex PhonePattern();

    private static ContactRequestListItemDto MapList(ContactRequest item) =>
        new(
            item.Id,
            item.FullName,
            item.Email,
            item.PhoneNumber,
            item.CompanyName,
            item.Subject,
            item.Status,
            item.CreatedAt);

    private static ContactRequestDetailDto MapDetail(ContactRequest item) =>
        new(
            item.Id,
            item.FullName,
            item.Email,
            item.PhoneNumber,
            item.CompanyName,
            item.Subject,
            item.Message,
            item.Status,
            item.ResolutionNote,
            item.ResolvedBy,
            item.ResolvedAt,
            item.CreatedAt,
            item.UpdatedAt,
            item.StatusHistory
                .OrderBy(history => history.CreatedAt)
                .Select(history => new ContactRequestStatusHistoryDto(
                    history.Id,
                    history.FromStatus,
                    history.ToStatus,
                    history.Note,
                    history.ChangedBy,
                    history.CreatedAt))
                .ToArray(),
            GetAllowedTransitions(item.Status));
}
