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
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
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

            return StatusCode(StatusCodes.Status201Created, result);
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
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var actorId))
            return Task.FromResult<IActionResult>(Unauthorized());

        return Execute(async () => Ok(await contactRequestService.UpdateStatusAsync(
            id,
            request,
            actorId,
            GetIpAddress(),
            cancellationToken)));
    }

    private Guid? GetOptionalUserId() =>
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            ? userId
            : null;

    private string? GetIpAddress() =>
        // In proxy deployments, Program.cs must run UseForwardedHeaders before
        // UseRateLimiter so this value is the trusted forwarded client address.
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
        catch (ContactRequestLockUnavailableException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Contact request queue is busy",
                detail: ex.Message);
        }
        catch (ContactRequestValidationException ex)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Validation failed",
                detail: ex.Message);
        }
    }
}
