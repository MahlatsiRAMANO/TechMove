using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.Auth;
using TechMove.Api.Dtos;
using TechMove.Api.Services;

namespace TechMove.Api.Controllers;

[ApiController]
[Route("api/service-requests")]
[Authorize]
[Produces("application/json")]
public sealed class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _requests;
    private readonly IContractService _contracts;
    private readonly ICurrentUser _user;

    public ServiceRequestsController(IServiceRequestService requests, IContractService contracts, ICurrentUser user)
    {
        _requests = requests;
        _contracts = contracts;
        _user = user;
    }

    /// <summary>List service requests, newest first. Customers only see their own company's requests.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ServiceRequestDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ServiceRequestDto>>> GetAll(CancellationToken ct) =>
        Ok((await _requests.GetAllAsync(_user.ClientScope, ct)).Select(s => DtoMapper.ToRequestDto(s)));

    /// <summary>Get one service request.</summary>
    [HttpGet("{id:int}", Name = nameof(GetServiceRequest))]
    [ProducesResponseType(typeof(ServiceRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceRequestDto>> GetServiceRequest(int id, CancellationToken ct)
    {
        var request = await _requests.GetAsync(id, ct);
        return request?.Contract is null || !_user.CanAccessClient(request.Contract.ClientId)
            ? NotFound()
            : Ok(DtoMapper.ToRequestDto(request));
    }

    /// <summary>Create a service request. The USD cost is converted to ZAR with the live exchange rate.</summary>
    /// <response code="201">Created - the response includes the rate used and the ZAR cost.</response>
    /// <response code="400">Validation failed, or the contract is Expired / On Hold.</response>
    /// <response code="503">The currency exchange API is unavailable.</response>
    [HttpPost]
    [ProducesResponseType(typeof(ServiceRequestDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<ServiceRequestDto>> Create(ServiceRequestCreateDto dto, CancellationToken ct)
    {
        // Customers may only log requests against their own company's contracts.
        if (_user.ClientScope.HasValue)
        {
            var contract = await _contracts.GetDetailsAsync(dto.ContractId!.Value, ct);
            if (contract is null || !_user.CanAccessClient(contract.ClientId))
            {
                throw new NotFoundException($"Contract {dto.ContractId} was not found.");
            }
        }

        var created = await _requests.CreateAsync(dto.ContractId!.Value, dto.Description, dto.CostUsd!.Value, ct);
        var details = await _requests.GetAsync(created.Id, ct) ?? created;
        return CreatedAtRoute(nameof(GetServiceRequest), new { id = created.Id }, DtoMapper.ToRequestDto(details));
    }

    /// <summary>Change the status of a service request (Pending → In Progress → Completed / Cancelled). Admin/Staff only.</summary>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = Roles.Employees)]
    [ProducesResponseType(typeof(ServiceRequestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ServiceRequestDto>> ChangeStatus(int id, ServiceRequestStatusUpdateDto dto, CancellationToken ct)
    {
        await _requests.UpdateStatusAsync(id, dto.Status!.Value, ct);
        return Ok(DtoMapper.ToRequestDto((await _requests.GetAsync(id, ct))!));
    }
}
