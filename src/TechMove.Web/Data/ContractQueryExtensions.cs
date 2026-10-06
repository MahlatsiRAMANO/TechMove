using TechMove.Web.Models;

namespace TechMove.Web.Data;

/// <summary>
/// LINQ search/filter for contracts. Written against IQueryable so EF Core translates it to SQL,
/// and so the same logic can be unit tested against an in-memory list.
/// </summary>
public static class ContractQueryExtensions
{
    public static IQueryable<Contract> ApplyFilter(this IQueryable<Contract> query, ContractSearchCriteria? criteria)
    {
        if (criteria is null)
        {
            return query.OrderByDescending(c => c.StartDate);
        }

        DateTime? from = criteria.FromDate?.Date;
        DateTime? to = criteria.ToDate?.Date;

        // Edge case: if the user enters the range backwards, swap rather than return nothing.
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            (from, to) = (to, from);
        }

        // Date range = contracts that are in force at any point within [from, to] (overlap test).
        if (from.HasValue)
        {
            var f = from.Value;
            query = query.Where(c => c.EndDate >= f);
        }

        if (to.HasValue)
        {
            var t = to.Value;
            query = query.Where(c => c.StartDate <= t);
        }

        if (criteria.Status.HasValue)
        {
            var status = criteria.Status.Value;
            query = query.Where(c => c.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(criteria.ClientName))
        {
            var name = criteria.ClientName.Trim();
            query = query.Where(c => c.Client != null && c.Client.Name.Contains(name));
        }

        return query.OrderByDescending(c => c.StartDate).ThenBy(c => c.Id);
    }
}
