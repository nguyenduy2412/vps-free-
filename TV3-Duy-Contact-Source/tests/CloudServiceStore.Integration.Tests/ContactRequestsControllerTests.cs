using System.Security.Claims;
using CloudServiceStore.Application.Common;
using CloudServiceStore.Application.ContactRequests;
using CloudServiceStore.Domain.Enums;
using CloudServiceStore.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CloudServiceStore.Integration.Tests;

public sealed class ContactRequestsControllerTests
{
    [Fact]
    public async Task Create_returns_201_without_advertising_the_protected_detail_route()
    {
        var service = new Mock<IContactRequestService>();
        var id = Guid.NewGuid();
        var ownerId = Guid.NewGuid();
        service.Setup(x => x.CreateAsync(
                It.IsAny<CreateContactRequestRequest>(),
                ownerId,
                "127.0.0.1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContactRequestConfirmationDto(id, ContactRequestStatus.Pending, DateTimeOffset.UtcNow));
        var controller = CreateController(service, ownerId, "127.0.0.1");

        var result = await controller.Create(ValidRequest(), CancellationToken.None);

        var created = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, created.StatusCode);
        var payload = Assert.IsType<ContactRequestConfirmationDto>(created.Value);
        Assert.Equal(id, payload.Id);
    }

    [Fact]
    public async Task Create_returns_409_problem_details_for_duplicate_request()
    {
        var service = new Mock<IContactRequestService>();
        service.Setup(x => x.CreateAsync(
                It.IsAny<CreateContactRequestRequest>(),
                null,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ContactRequestConflictException("Duplicate request."));
        var controller = CreateController(service, null, "127.0.0.1");

        var result = await controller.Create(ValidRequest(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("Business conflict", Assert.IsType<ProblemDetails>(problem.Value).Title);
    }

    [Fact]
    public async Task Create_returns_503_problem_details_when_app_lock_is_unavailable()
    {
        var service = new Mock<IContactRequestService>();
        service.Setup(x => x.CreateAsync(
                It.IsAny<CreateContactRequestRequest>(),
                null,
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ContactRequestLockUnavailableException("Queue timeout."));
        var controller = CreateController(service, null, "127.0.0.1");

        var result = await controller.Create(ValidRequest(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, problem.StatusCode);
        Assert.Equal("Contact request queue is busy", Assert.IsType<ProblemDetails>(problem.Value).Title);
    }

    [Fact]
    public async Task Get_returns_paged_result_from_service()
    {
        var service = new Mock<IContactRequestService>();
        var query = new ContactRequestQuery(Page: 2, PageSize: 10, Status: ContactRequestStatus.Pending);
        service.Setup(x => x.GetAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResult<ContactRequestListItemDto>([], 2, 10, 0));
        var controller = CreateController(service);

        var result = await controller.Get(query, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.IsType<PagedResult<ContactRequestListItemDto>>(ok.Value);
    }

    [Fact]
    public async Task GetById_returns_404_when_service_returns_null()
    {
        var service = new Mock<IContactRequestService>();
        service.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactRequestDetailDto?)null);
        var controller = CreateController(service);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status404NotFound, problem.StatusCode);
    }

    [Fact]
    public async Task GetById_returns_200_and_detail_when_service_returns_request()
    {
        var service = new Mock<IContactRequestService>();
        var id = Guid.NewGuid();
        var expected = Detail(id, ContactRequestStatus.Pending);
        service.Setup(x => x.GetByIdAsync(id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);
        var controller = CreateController(service);

        var result = await controller.GetById(id, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<ContactRequestDetailDto>(ok.Value);
        Assert.Equal(expected.Id, payload.Id);
        Assert.Equal(expected.Status, payload.Status);
    }

    [Fact]
    public async Task GetById_returns_409_problem_details_when_service_throws_conflict()
    {
        var service = new Mock<IContactRequestService>();
        service.Setup(x => x.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ContactRequestConflictException("Query conflict."));
        var controller = CreateController(service);

        var result = await controller.GetById(Guid.NewGuid(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, problem.StatusCode);
        Assert.Equal("Business conflict", Assert.IsType<ProblemDetails>(problem.Value).Title);
    }

    [Fact]
    public async Task UpdateStatus_passes_claim_actor_and_ip_to_service()
    {
        var service = new Mock<IContactRequestService>();
        var id = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var request = new UpdateContactRequestStatusRequest(ContactRequestStatus.Contacted, "Đã liên hệ.");
        service.Setup(x => x.UpdateStatusAsync(
                id,
                request,
                actorId,
                "10.0.0.8",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(Detail(id, ContactRequestStatus.Contacted));
        var controller = CreateController(service, actorId, "10.0.0.8");

        var result = await controller.UpdateStatus(id, request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(ContactRequestStatus.Contacted, Assert.IsType<ContactRequestDetailDto>(ok.Value).Status);
        service.Verify(x => x.UpdateStatusAsync(id, request, actorId, "10.0.0.8", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_returns_400_problem_details_for_validation_error()
    {
        var service = new Mock<IContactRequestService>();
        service.Setup(x => x.UpdateStatusAsync(
                It.IsAny<Guid>(),
                It.IsAny<UpdateContactRequestStatusRequest>(),
                It.IsAny<Guid>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ContactRequestValidationException("Invalid transition."));
        var controller = CreateController(service, Guid.NewGuid());

        var result = await controller.UpdateStatus(
            Guid.NewGuid(),
            new(ContactRequestStatus.Rejected, null),
            CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.StatusCode);
        Assert.Equal("Validation failed", Assert.IsType<ProblemDetails>(problem.Value).Title);
    }

    [Fact]
    public async Task UpdateStatus_returns_401_when_name_identifier_claim_is_missing()
    {
        var service = new Mock<IContactRequestService>();
        var controller = CreateController(service);

        var result = await controller.UpdateStatus(
            Guid.NewGuid(),
            new(ContactRequestStatus.Contacted, "Đã liên hệ."),
            CancellationToken.None);

        Assert.IsType<UnauthorizedResult>(result);
        service.Verify(x => x.UpdateStatusAsync(
            It.IsAny<Guid>(),
            It.IsAny<UpdateContactRequestStatusRequest>(),
            It.IsAny<Guid>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void Create_declares_rate_limit_and_queue_problem_details_responses()
    {
        var action = typeof(ContactRequestsController).GetMethod(nameof(ContactRequestsController.Create));
        Assert.NotNull(action);

        var responses = action.GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: true)
            .Cast<ProducesResponseTypeAttribute>();

        Assert.Contains(responses, response =>
            response.StatusCode == StatusCodes.Status429TooManyRequests
            && response.Type == typeof(ProblemDetails));
        Assert.Contains(responses, response =>
            response.StatusCode == StatusCodes.Status503ServiceUnavailable
            && response.Type == typeof(ProblemDetails));
    }

    [Theory]
    [InlineData(nameof(ContactRequestsController.Get))]
    [InlineData(nameof(ContactRequestsController.GetById))]
    [InlineData(nameof(ContactRequestsController.UpdateStatus))]
    public void Protected_actions_declare_401_and_403_problem_details_responses(string actionName)
    {
        var action = typeof(ContactRequestsController).GetMethod(actionName);
        Assert.NotNull(action);

        var statusCodes = action.GetCustomAttributes(typeof(ProducesResponseTypeAttribute), inherit: true)
            .Cast<ProducesResponseTypeAttribute>()
            .Where(response => response.Type == typeof(ProblemDetails))
            .Select(response => response.StatusCode)
            .ToArray();

        Assert.Contains(StatusCodes.Status401Unauthorized, statusCodes);
        Assert.Contains(StatusCodes.Status403Forbidden, statusCodes);
    }

    private static ContactRequestsController CreateController(
        Mock<IContactRequestService> service,
        Guid? userId = null,
        string? ipAddress = null)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = ipAddress is null
            ? null
            : System.Net.IPAddress.Parse(ipAddress);

        if (userId is { } id)
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, id.ToString())],
                "Test"));
        }

        return new ContactRequestsController(service.Object)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };
    }

    private static CreateContactRequestRequest ValidRequest() =>
        new("Nguyen Phuoc Duy", "duy@example.com", "0901234567", null,
            "Tư vấn cloud", "Tôi cần tư vấn dịch vụ cloud cho doanh nghiệp.");

    private static ContactRequestDetailDto Detail(Guid id, ContactRequestStatus status) =>
        new(id, "Nguyen Phuoc Duy", "duy@example.com", "0901234567", null,
            "Tư vấn cloud", "Tôi cần tư vấn dịch vụ cloud cho doanh nghiệp.",
            status, null, null, null, DateTimeOffset.UtcNow, null, [], []);
}
