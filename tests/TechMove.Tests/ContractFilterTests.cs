using TechMove.Web.Data;
using TechMove.Web.Models;

namespace TechMove.Tests;

/// <summary>Tests the LINQ search/filter used by the admin contract search.</summary>
public class ContractFilterTests
{
    private static readonly Client Acme = new() { Id = 1, Name = "Acme Logistics", ContactDetails = "x", Region = "Gauteng" };
    private static readonly Client Cape = new() { Id = 2, Name = "Cape Freight", ContactDetails = "x", Region = "Western Cape" };

    private static IQueryable<Contract> Data() => new List<Contract>
    {
        new() { Id = 1, Client = Acme, ClientId = 1, StartDate = new(2025, 1, 1), EndDate = new(2025, 12, 31), Status = ContractStatus.Expired },
        new() { Id = 2, Client = Acme, ClientId = 1, StartDate = new(2026, 1, 1), EndDate = new(2026, 12, 31), Status = ContractStatus.Active },
        new() { Id = 3, Client = Cape, ClientId = 2, StartDate = new(2026, 6, 1), EndDate = new(2027, 5, 31), Status = ContractStatus.OnHold },
        new() { Id = 4, Client = Cape, ClientId = 2, StartDate = new(2027, 1, 1), EndDate = new(2027, 12, 31), Status = ContractStatus.Draft },
    }.AsQueryable();

    private static int[] Ids(IQueryable<Contract> q) => q.Select(c => c.Id).OrderBy(i => i).ToArray();

    [Fact]
    public void NoCriteria_ReturnsAll()
    {
        Assert.Equal(new[] { 1, 2, 3, 4 }, Ids(Data().ApplyFilter(new ContractSearchCriteria())));
    }

    [Fact]
    public void NullCriteria_ReturnsAll()
    {
        Assert.Equal(4, Data().ApplyFilter(null).Count());
    }

    [Fact]
    public void ByStatus_ReturnsOnlyThatStatus()
    {
        var result = Data().ApplyFilter(new ContractSearchCriteria { Status = ContractStatus.Active });

        Assert.Equal(new[] { 2 }, Ids(result));
    }

    [Fact]
    public void ByDateRange_ReturnsContractsInForceDuringRange()
    {
        // 2026-07-01..2026-08-31 overlaps contract 2 (all 2026) and 3 (from June 2026).
        var result = Data().ApplyFilter(new ContractSearchCriteria
        {
            FromDate = new DateTime(2026, 7, 1),
            ToDate = new DateTime(2026, 8, 31)
        });

        Assert.Equal(new[] { 2, 3 }, Ids(result));
    }

    [Fact]
    public void ByDateRangeAndStatus_Combines()
    {
        var result = Data().ApplyFilter(new ContractSearchCriteria
        {
            FromDate = new DateTime(2026, 1, 1),
            ToDate = new DateTime(2027, 12, 31),
            Status = ContractStatus.OnHold
        });

        Assert.Equal(new[] { 3 }, Ids(result));
    }

    [Fact]
    public void ReversedDateRange_IsSwappedNotEmpty()
    {
        var result = Data().ApplyFilter(new ContractSearchCriteria
        {
            FromDate = new DateTime(2026, 8, 31),
            ToDate = new DateTime(2026, 7, 1)
        });

        Assert.Equal(new[] { 2, 3 }, Ids(result));
    }

    [Fact]
    public void FromDateOnly_ExcludesContractsThatEndedBefore()
    {
        var result = Data().ApplyFilter(new ContractSearchCriteria { FromDate = new DateTime(2026, 1, 1) });

        Assert.Equal(new[] { 2, 3, 4 }, Ids(result));
    }

    [Fact]
    public void ByClientName_MatchesPartialName()
    {
        var result = Data().ApplyFilter(new ContractSearchCriteria { ClientName = " Cape " });

        Assert.Equal(new[] { 3, 4 }, Ids(result));
    }

    [Fact]
    public void Results_AreNewestFirst()
    {
        var ids = Data().ApplyFilter(new ContractSearchCriteria()).Select(c => c.Id).ToArray();

        Assert.Equal(new[] { 4, 3, 2, 1 }, ids);
    }
}
