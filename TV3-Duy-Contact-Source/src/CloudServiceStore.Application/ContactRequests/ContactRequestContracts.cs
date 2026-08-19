using System.ComponentModel.DataAnnotations;
using CloudServiceStore.Application.Common;
using CloudServiceStore.Domain.Enums;

namespace CloudServiceStore.Application.ContactRequests;

public sealed record CreateContactRequestRequest(
    [param: Required, StringLength(160, MinimumLength = 2)] string FullName,
    [param: Required, EmailAddress, StringLength(256)] string Email,
    [param: Required, RegularExpression(@"^\+?(?:[\d][\s().-]*){8,15}$")] string PhoneNumber,
    [param: StringLength(160)] string? CompanyName,
    [param: Required, StringLength(180, MinimumLength = 3)] string Subject,
    [param: Required, StringLength(4000, MinimumLength = 10)] string Message);

public sealed record ContactRequestQuery(
    [param: Range(1, int.MaxValue)] int Page = 1,
    [param: Range(1, 100)] int PageSize = 20,
    [param: StringLength(256)] string? Search = null,
    ContactRequestStatus? Status = null,
    DateTimeOffset? CreatedFrom = null,
    DateTimeOffset? CreatedTo = null);

public sealed record UpdateContactRequestStatusRequest(
    ContactRequestStatus Status,
    [param: StringLength(1000)] string? Note);

public sealed record ContactRequestConfirmationDto(
    Guid Id,
    ContactRequestStatus Status,
    DateTimeOffset CreatedAt);

public sealed record ContactRequestListItemDto(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string? CompanyName,
    string Subject,
    ContactRequestStatus Status,
    DateTimeOffset CreatedAt);

public sealed record ContactRequestStatusHistoryDto(
    Guid Id,
    ContactRequestStatus FromStatus,
    ContactRequestStatus ToStatus,
    string? Note,
    Guid? ChangedBy,
    DateTimeOffset CreatedAt);

public sealed record ContactRequestDetailDto(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string? CompanyName,
    string Subject,
    string Message,
    ContactRequestStatus Status,
    string? ResolutionNote,
    Guid? ResolvedBy,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<ContactRequestStatusHistoryDto> StatusHistory,
    IReadOnlyList<ContactRequestStatus> AllowedTransitions);

public interface IContactRequestService
{
    Task<ContactRequestConfirmationDto> CreateAsync(
        CreateContactRequestRequest request,
        Guid? ownerId,
        string? ipAddress,
        CancellationToken cancellationToken);

    Task<PagedResult<ContactRequestListItemDto>> GetAsync(
        ContactRequestQuery query,
        CancellationToken cancellationToken);

    Task<ContactRequestDetailDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<ContactRequestDetailDto> UpdateStatusAsync(
        Guid id,
        UpdateContactRequestStatusRequest request,
        Guid actorId,
        string? ipAddress,
        CancellationToken cancellationToken);
}
