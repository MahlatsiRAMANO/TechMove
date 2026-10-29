using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TechMove.Api.Dtos;
using TechMove.Api.Models;
using TechMove.Api.Services;

namespace TechMove.Api.Controllers;

[ApiController]
[Route("api/clients")]
[Authorize]
[Produces("application/json")]
public sealed class ClientsController : ControllerBase
{
    private readonly IClientService _clients;
    private readonly DtoMapper _mapper;

    public ClientsController(IClientService clients, DtoMapper mapper)
    {
        _clients = clients;
        _mapper = mapper;
    }

    /// <summary>List all clients, ordered by name.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClientDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<ClientDto>>> GetAll(CancellationToken ct) =>
        Ok((await _clients.GetAllAsync(ct)).Select(c => _mapper.ToDto(c)));

    /// <summary>Get one client with its contracts.</summary>
    [HttpGet("{id:int}", Name = nameof(GetClient))]
    [ProducesResponseType(typeof(ClientDetailsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDetailsDto>> GetClient(int id, CancellationToken ct)
    {
        var client = await _clients.GetAsync(id, ct);
        return client is null ? NotFound() : Ok(_mapper.ToDetailsDto(client));
    }

    /// <summary>Create a client.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ClientDto>> Create(ClientWriteDto dto, CancellationToken ct)
    {
        var created = await _clients.CreateAsync(new Client { Name = dto.Name, ContactDetails = dto.ContactDetails, Region = dto.Region }, ct);
        return CreatedAtRoute(nameof(GetClient), new { id = created.Id }, _mapper.ToDto(created));
    }

    /// <summary>Update a client.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, ClientWriteDto dto, CancellationToken ct)
    {
        await _clients.UpdateAsync(new Client { Id = id, Name = dto.Name, ContactDetails = dto.ContactDetails, Region = dto.Region }, ct);
        return NoContent();
    }

    /// <summary>Delete a client (only allowed when it has no contracts).</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await _clients.DeleteAsync(id, ct);
        return NoContent();
    }
}
