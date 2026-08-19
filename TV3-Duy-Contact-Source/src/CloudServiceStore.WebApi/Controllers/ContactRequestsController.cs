using System.Security.Claims;
using CloudServiceStore.Application.Common;
using CloudServiceStore.Application.ContactRequests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using CloudServiceStore.WebApi.Security;

namespace CloudServiceStore.WebApi.Controllers;

[ApiController]
[Route("api/v1/contact-requests")]
public sealed class ContactRequestsController(
    IContactRequestService contactRequestService) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost]
    [EnableRateLimiting(ContactRequestRateLimitExtensions.PolicyName)]
    [ProducesResponseType(typeof(ContactRequestConfirmationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public Task<IActionResult> Create(
        [FromBody] CreateContactRequestRequest request,
        CancellationToken cancellationToken) =>
        Execute(async () =>
        {
            var result = await contactRequestService.CreateAsync(
                request,
                GetOptionalUserId(),
                GetIpAddress(),
                cancellationToken);

            return CreatedAtAction(
                nameof(GetById),
                new { id = result.Id },
                result);
        });

    [Authorize(Policy = "ManageContactRequests")]
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ContactRequestListItemDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Get(
        [FromQuery] ContactRequestQuery query,
        CancellationToken cancellationToken) =>
        Execute(async () => Ok(await contactRequestService.GetAsync(query, cancellationToken)));

    [Authorize(Policy = "ManageContactRequests")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ContactRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public Task<IActionResult> GetById(
        Guid id,
        CancellationToken cancellationToken) =>
        Execute(async () =>
        {
            var result = await contactRequestService.GetByIdAsync(id, cancellationToken);
            return result is null
                ? Problem(
                    statusCode: StatusCodes.Status404NotFound,
                    title: "Resource not found")
                : Ok(result);
        });

    [Authorize(Policy = "ManageContactRequests")]
    [HttpPost("{id:guid}/status")]
    [ProducesResponseType(typeof(ContactRequestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public Task<IActionResult> UpdateStatus(
        Guid id,
        [FromBody] UpdateContactRequestStatusRequest request,
        CancellationToken cancellationToken) =>
        Execute(async () => Ok(await contactRequestService.UpdateStatusAsync(
            id,
            request,
            GetActorId(),
            GetIpAddress(),
            cancellationToken)));

    private Guid? GetOptionalUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

    private Guid GetActorId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId)
            ? actorId
            : throw new UnauthorizedAccessException();

    private string? GetIpAddress() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();

    private async Task<IActionResult> Execute(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ContactRequestNotFoundException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status404NotFound,
                title: "Resource not found",
                detail: ex.Message);
        }
        catch (ContactRequestConflictException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "Business conflict",
                detail: ex.Message);
        }
        catch (ContactRequestValidationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed",
                detail: ex.Message);
        }
        catch (UnauthorizedAccessException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status401Unauthorized,
                title: "Unauthorized",
                detail: ex.Message);
        }
    }
}
