using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.Auth;
using TechMove.Api.Dtos;
using TechMove.Api.Models;
using TechMove.Api.Services;
using TechMove.Api.Services.Files;

namespace TechMove.Api.Controllers;

/// <summary>Contracts, their status workflow and the signed agreement PDF.</summary>
[ApiController]
[Route("api/contracts")]
[Authorize]
[Produces("application/json")]
public sealed class ContractsController : ControllerBase
{
    private const long UploadLimit = PdfFileValidator.MaxFileSizeBytes + 1024 * 1024;

    private readonly IContractService _contracts;
    private readonly DtoMapper _mapper;
    private readonly IDateTimeProvider _clock;
    private readonly ICurrentUser _user;

    public ContractsController(IContractService contracts, DtoMapper mapper, IDateTimeProvider clock, ICurrentUser user)
    {
        _contracts = contracts;
        _mapper = mapper;
        _clock = clock;
        _user = user;
    }

    /// <summary>Search contracts. All filters are optional. Customers only ever see their own company's contracts.</summary>
    /// <remarks>Example: GET /api/contracts?status=Active&amp;fromDate=2026-01-01&amp;toDate=2026-12-31&amp;clientName=acme</remarks>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ContractDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ContractDto>>> Search([FromQuery] ContractSearchCriteria criteria, CancellationToken ct)
    {
        if (_user.ClientScope.HasValue)
        {
            criteria.ClientId = _user.ClientScope; // customers cannot widen the search to other clients
        }

        var today = _clock.Today;
        var contracts = await _contracts.SearchAsync(criteria, ct);
        return Ok(contracts.Select(c => _mapper.ToDto(c, today)));
    }

    /// <summary>Contracts that may currently take new service requests (Draft/Active and not past their end date).</summary>
    [HttpGet("open")]
    [ProducesResponseType(typeof(IEnumerable<ContractDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ContractDto>>> GetOpen(CancellationToken ct)
    {
        var today = _clock.Today;
        var open = (await _contracts.GetOpenForServiceRequestsAsync(ct)).Where(c => _user.CanAccessClient(c.ClientId));
        return Ok(open.Select(c => _mapper.ToDto(c, today)));
    }

    /// <summary>Get one contract with its client and service requests.</summary>
    [HttpGet("{id:int}", Name = nameof(GetContract))]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractDto>> GetContract(int id, CancellationToken ct)
    {
        var contract = await _contracts.GetDetailsAsync(id, ct);

        // A customer asking for another company's contract gets 404, so they can't even tell it exists.
        return contract is null || !_user.CanAccessClient(contract.ClientId)
            ? NotFound()
            : Ok(_mapper.ToDto(contract, _clock.Today));
    }

    /// <summary>Create a contract (Admin/Staff). Send as multipart/form-data including the signed agreement PDF.</summary>
    [HttpPost]
    [Authorize(Roles = Roles.Employees)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ContractDto>> Create([FromForm] ContractCreateForm form, CancellationToken ct)
    {
        var entity = new Contract
        {
            ClientId = form.ClientId!.Value,
            StartDate = form.StartDate!.Value.Date,
            EndDate = form.EndDate!.Value.Date,
            Status = form.Status,
            ServiceLevel = form.ServiceLevel
        };

        var created = await _contracts.CreateAsync(entity, form.SignedAgreement, ct);
        var details = await _contracts.GetDetailsAsync(created.Id, ct) ?? created;
        return CreatedAtRoute(nameof(GetContract), new { id = created.Id }, _mapper.ToDto(details, _clock.Today));
    }

    /// <summary>Update a contract's client, dates and service level. Use PATCH /status to change the status.</summary>
    [HttpPut("{id:int}")]
    [Authorize(Roles = Roles.Employees)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, ContractUpdateDto dto, CancellationToken ct)
    {
        var current = await _contracts.GetDetailsAsync(id, ct)
                      ?? throw new NotFoundException($"Contract {id} was not found.");

        await _contracts.UpdateAsync(new Contract
        {
            Id = id,
            ClientId = dto.ClientId!.Value,
            StartDate = dto.StartDate!.Value.Date,
            EndDate = dto.EndDate!.Value.Date,
            Status = current.Status,
            ServiceLevel = dto.ServiceLevel
        }, ct);
        return NoContent();
    }

    /// <summary>Approve / decline a contract by moving it through the workflow (Admin only).</summary>
    /// <remarks>Body: { "status": "Active" }. Allowed moves: Draft→Active/OnHold/Expired, Active→OnHold/Expired, OnHold→Active/Expired. Expired is final.</remarks>
    [HttpPatch("{id:int}/status")]
    [Authorize(Roles = Roles.Admin)]
    [ProducesResponseType(typeof(ContractDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ContractDto>> ChangeStatus(int id, ContractStatusUpdateDto dto, CancellationToken ct)
    {
        await _contracts.ChangeStatusAsync(id, dto.Status!.Value, ct);
        var details = await _contracts.GetDetailsAsync(id, ct);
        return Ok(_mapper.ToDto(details!, _clock.Today));
    }

    /// <summary>Upload (or replace) the signed agreement PDF (Admin/Staff).</summary>
    [HttpPost("{id:int}/agreement")]
    [Authorize(Roles = Roles.Employees)]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(UploadLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadLimit)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UploadAgreement(int id, IFormFile? signedAgreement, CancellationToken ct)
    {
        await _contracts.UploadAgreementAsync(id, signedAgreement, ct);
        return NoContent();
    }

    /// <summary>Download the signed agreement PDF.</summary>
    [HttpGet("{id:int}/agreement")]
    [Produces("application/pdf", "application/json")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadAgreement(int id, CancellationToken ct)
    {
        var contract = await _contracts.GetDetailsAsync(id, ct);
        if (contract is null || !_user.CanAccessClient(contract.ClientId))
        {
            return NotFound();
        }

        var file = await _contracts.GetAgreementAsync(id, ct);
        return File(file.Content, "application/pdf", file.FileName);
    }
}
