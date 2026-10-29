using Microsoft.EntityFrameworkCore;
using TechMove.Api.Models;

namespace TechMove.Api.Data.Repositories;

// ----------------------------------------------------------------------------------------------
// Repository pattern: the ONLY classes that touch AppDbContext. Controllers never see EF Core.
// ----------------------------------------------------------------------------------------------

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Update(T entity);
    void Remove(T entity);
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> Set;

    public Repository(AppDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        await Set.FindAsync(new object[] { id }, ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    public virtual void Update(T entity) => Set.Update(entity);

    public virtual void Remove(T entity) => Set.Remove(entity);

    public Task<int> SaveChangesAsync(CancellationToken ct = default) => Context.SaveChangesAsync(ct);
}

public interface IClientRepository : IRepository<Client>
{
    Task<IReadOnlyList<Client>> GetAllOrderedAsync(CancellationToken ct = default);
    Task<Client?> GetWithContractsAsync(int id, CancellationToken ct = default);
}

public class ClientRepository : Repository<Client>, IClientRepository
{
    public ClientRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<Client>> GetAllOrderedAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().Include(c => c.Contracts).OrderBy(c => c.Name).ToListAsync(ct);

    public Task<Client?> GetWithContractsAsync(int id, CancellationToken ct = default) =>
        Set.Include(c => c.Contracts).FirstOrDefaultAsync(c => c.Id == id, ct);
}

public interface IContractRepository : IRepository<Contract>
{
    Task<Contract?> GetWithDetailsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Contract>> SearchAsync(ContractSearchCriteria? criteria, CancellationToken ct = default);
    Task<IReadOnlyList<Contract>> GetByStatusesAsync(IEnumerable<ContractStatus> statuses, CancellationToken ct = default);
    Task<int> CountByStatusAsync(ContractStatus status, CancellationToken ct = default) => CountByStatusAsync(status, null, ct);
    Task<int> CountByStatusAsync(ContractStatus status, int? clientId, CancellationToken ct = default);
}

public class ContractRepository : Repository<Contract>, IContractRepository
{
    public ContractRepository(AppDbContext context) : base(context) { }

    public Task<Contract?> GetWithDetailsAsync(int id, CancellationToken ct = default) =>
        Set.Include(c => c.Client)
           .Include(c => c.ServiceRequests)
           .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<IReadOnlyList<Contract>> SearchAsync(ContractSearchCriteria? criteria, CancellationToken ct = default) =>
        await Set.AsNoTracking()
                 .Include(c => c.Client)
                 .ApplyFilter(criteria)
                 .ToListAsync(ct);

    public async Task<IReadOnlyList<Contract>> GetByStatusesAsync(IEnumerable<ContractStatus> statuses, CancellationToken ct = default)
    {
        var list = statuses.ToList();
        return await Set.AsNoTracking()
                        .Include(c => c.Client)
                        .Where(c => list.Contains(c.Status))
                        .OrderBy(c => c.Client!.Name)
                        .ToListAsync(ct);
    }

    public Task<int> CountByStatusAsync(ContractStatus status, int? clientId, CancellationToken ct = default) =>
        clientId.HasValue
            ? Set.CountAsync(c => c.Status == status && c.ClientId == clientId.Value, ct)
            : Set.CountAsync(c => c.Status == status, ct);
}

public interface IServiceRequestRepository : IRepository<ServiceRequest>
{
    Task<IReadOnlyList<ServiceRequest>> GetAllWithContractAsync(CancellationToken ct = default) => GetAllWithContractAsync(null, ct);
    Task<IReadOnlyList<ServiceRequest>> GetAllWithContractAsync(int? clientId, CancellationToken ct = default);
    Task<ServiceRequest?> GetWithContractAsync(int id, CancellationToken ct = default);
    Task<decimal> TotalCostAsync(CancellationToken ct = default) => TotalCostAsync(null, ct);
    Task<decimal> TotalCostAsync(int? clientId, CancellationToken ct = default);
    Task<int> CountAsync(CancellationToken ct = default) => CountAsync(null, ct);
    Task<int> CountAsync(int? clientId, CancellationToken ct = default);
}

public class ServiceRequestRepository : Repository<ServiceRequest>, IServiceRequestRepository
{
    public ServiceRequestRepository(AppDbContext context) : base(context) { }

    public async Task<IReadOnlyList<ServiceRequest>> GetAllWithContractAsync(int? clientId, CancellationToken ct = default) =>
        await Scoped(clientId).AsNoTracking()
                 .Include(s => s.Contract!).ThenInclude(c => c.Client)
                 .OrderByDescending(s => s.CreatedAtUtc)
                 .ToListAsync(ct);

    public Task<ServiceRequest?> GetWithContractAsync(int id, CancellationToken ct = default) =>
        Set.Include(s => s.Contract!).ThenInclude(c => c.Client)
           .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<decimal> TotalCostAsync(int? clientId, CancellationToken ct = default) =>
        await Scoped(clientId).SumAsync(s => (decimal?)s.Cost, ct) ?? 0m;

    public Task<int> CountAsync(int? clientId, CancellationToken ct = default) => Scoped(clientId).CountAsync(ct);

    private IQueryable<ServiceRequest> Scoped(int? clientId) =>
        clientId.HasValue ? Set.Where(s => s.Contract!.ClientId == clientId.Value) : Set;
}

public interface IUserRepository : IRepository<AppUser>
{
    Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<IReadOnlyList<AppUser>> GetAllWithClientAsync(CancellationToken ct = default);
    Task<int> CountByRoleAsync(UserRole role, CancellationToken ct = default);
}

public class UserRepository : Repository<AppUser>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context) { }

    public Task<AppUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalised = username.Trim().ToLower();
        return Set.Include(u => u.Client).FirstOrDefaultAsync(u => u.Username.ToLower() == normalised, ct);
    }

    public async Task<IReadOnlyList<AppUser>> GetAllWithClientAsync(CancellationToken ct = default) =>
        await Set.AsNoTracking().Include(u => u.Client).OrderBy(u => u.Role).ThenBy(u => u.Username).ToListAsync(ct);

    public Task<int> CountByRoleAsync(UserRole role, CancellationToken ct = default) =>
        Set.CountAsync(u => u.Role == role, ct);
}
