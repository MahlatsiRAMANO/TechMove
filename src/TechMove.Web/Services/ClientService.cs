using TechMove.Web.Data.Repositories;
using TechMove.Web.Models;

namespace TechMove.Web.Services;

public interface IClientService
{
    Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken ct = default);
    Task<Client?> GetAsync(int id, CancellationToken ct = default);
    Task<Client> CreateAsync(Client client, CancellationToken ct = default);
    Task UpdateAsync(Client client, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed class ClientService : IClientService
{
    private readonly IClientRepository _clients;

    public ClientService(IClientRepository clients) => _clients = clients;

    public Task<IReadOnlyList<Client>> GetAllAsync(CancellationToken ct = default) => _clients.GetAllOrderedAsync(ct);

    public Task<Client?> GetAsync(int id, CancellationToken ct = default) => _clients.GetWithContractsAsync(id, ct);

    public async Task<Client> CreateAsync(Client client, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        Normalise(client);
        await _clients.AddAsync(client, ct);
        await _clients.SaveChangesAsync(ct);
        return client;
    }

    public async Task UpdateAsync(Client client, CancellationToken ct = default)
    {
        var existing = await _clients.GetByIdAsync(client.Id, ct)
                       ?? throw new NotFoundException($"Client {client.Id} was not found.");
        Normalise(client);
        existing.Name = client.Name;
        existing.ContactDetails = client.ContactDetails;
        existing.Region = client.Region;
        await _clients.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var existing = await _clients.GetWithContractsAsync(id, ct)
                       ?? throw new NotFoundException($"Client {id} was not found.");

        if (existing.Contracts.Count > 0)
        {
            throw new BusinessRuleException("This client still has contracts and cannot be deleted.");
        }

        _clients.Remove(existing);
        await _clients.SaveChangesAsync(ct);
    }

    private static void Normalise(Client c)
    {
        c.Name = c.Name.Trim();
        c.ContactDetails = c.ContactDetails.Trim();
        c.Region = c.Region.Trim();
    }
}
