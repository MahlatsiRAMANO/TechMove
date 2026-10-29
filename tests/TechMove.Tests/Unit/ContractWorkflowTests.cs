using TechMove.Api.Models;
using TechMove.Api.Services;
using TechMove.Api.Services.Workflow;

namespace TechMove.Tests;

public class ContractWorkflowTests
{
    private static readonly DateTime Today = new(2026, 10, 6);
    private readonly ContractWorkflow _sut = new();

    private static Contract MakeContract(ContractStatus status, DateTime? end = null) => new()
    {
        Id = 1,
        ClientId = 1,
        StartDate = Today.AddMonths(-1),
        EndDate = end ?? Today.AddMonths(11),
        Status = status
    };

    [Theory]
    [InlineData(ContractStatus.Active)]
    [InlineData(ContractStatus.Draft)]
    public void CanCreateServiceRequest_ActiveOrDraft_ReturnsTrue(ContractStatus status)
    {
        Assert.True(_sut.CanCreateServiceRequest(MakeContract(status), Today));
    }

    [Theory]
    [InlineData(ContractStatus.Expired)]
    [InlineData(ContractStatus.OnHold)]
    public void EnsureCanCreateServiceRequest_ExpiredOrOnHold_Throws(ContractStatus status)
    {
        var ex = Assert.Throws<BusinessRuleException>(() =>
            _sut.EnsureCanCreateServiceRequest(MakeContract(status), Today));

        Assert.Contains("cannot be created", ex.Message);
    }

    [Fact]
    public void EnsureCanCreateServiceRequest_ActiveButEndDatePassed_Throws()
    {
        var contract = MakeContract(ContractStatus.Active, end: Today.AddDays(-1));

        Assert.Throws<BusinessRuleException>(() => _sut.EnsureCanCreateServiceRequest(contract, Today));
    }

    [Fact]
    public void CanCreateServiceRequest_EndDateIsToday_StillAllowed()
    {
        var contract = MakeContract(ContractStatus.Active, end: Today);

        Assert.True(_sut.CanCreateServiceRequest(contract, Today));
    }

    [Theory]
    [InlineData(ContractStatus.Draft, ContractStatus.Active)]
    [InlineData(ContractStatus.Active, ContractStatus.OnHold)]
    [InlineData(ContractStatus.OnHold, ContractStatus.Active)]
    [InlineData(ContractStatus.Active, ContractStatus.Expired)]
    [InlineData(ContractStatus.Active, ContractStatus.Active)] // no change is always fine
    public void CanTransition_AllowedMoves_ReturnTrue(ContractStatus from, ContractStatus to)
    {
        Assert.True(_sut.CanTransition(from, to));
    }

    [Theory]
    [InlineData(ContractStatus.Expired, ContractStatus.Active)]
    [InlineData(ContractStatus.Expired, ContractStatus.Draft)]
    [InlineData(ContractStatus.Active, ContractStatus.Draft)]
    [InlineData(ContractStatus.OnHold, ContractStatus.Draft)]
    public void EnsureCanTransition_IllegalMoves_Throw(ContractStatus from, ContractStatus to)
    {
        Assert.Throws<BusinessRuleException>(() => _sut.EnsureCanTransition(from, to));
    }

    [Fact]
    public void AllowedNextStatuses_Expired_OnlyItself()
    {
        var allowed = _sut.AllowedNextStatuses(ContractStatus.Expired);

        var only = Assert.Single(allowed);
        Assert.Equal(ContractStatus.Expired, only);
    }
}
