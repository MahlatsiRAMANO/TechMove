using Moq;
using TechMove.Api.Data.Repositories;
using TechMove.Api.Models;
using TechMove.Api.Services;
using TechMove.Api.Services.Currency;
using TechMove.Api.Services.Workflow;

namespace TechMove.Tests;

/// <summary>Service-layer tests using Moq so no database or internet is needed.</summary>
public class ServiceRequestServiceTests
{
    private static readonly DateTime Today = new(2026, 10, 6);

    private readonly Mock<IServiceRequestRepository> _requests = new();
    private readonly Mock<IContractRepository> _contracts = new();
    private readonly Mock<IExchangeRateProvider> _rates = new();

    private ServiceRequestService CreateSut() => new(
        _requests.Object,
        _contracts.Object,
        new ContractWorkflow(),
        _rates.Object,
        new CurrencyConverter(),
        new ServiceRequestFactory(),
        new FixedClock(Today));

    private void GivenContract(ContractStatus status, int id = 7)
    {
        _contracts.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>()))
                  .ReturnsAsync(new Contract
                  {
                      Id = id,
                      ClientId = 1,
                      StartDate = Today.AddMonths(-2),
                      EndDate = Today.AddMonths(10),
                      Status = status
                  });
    }

    private void GivenRate(decimal rate) =>
        _rates.Setup(r => r.GetUsdToZarRateAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(ExchangeRateResult.Ok(rate, Today));

    [Fact]
    public async Task CreateAsync_ActiveContract_ConvertsAndSaves()
    {
        GivenContract(ContractStatus.Active);
        GivenRate(18.50m);
        ServiceRequest? saved = null;
        _requests.Setup(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()))
                 .Callback<ServiceRequest, CancellationToken>((sr, _) => saved = sr)
                 .Returns(Task.CompletedTask);

        var result = await CreateSut().CreateAsync(7, "  Move 3 servers  ", 100m);

        Assert.NotNull(saved);
        Assert.Equal(1850.00m, result.Cost);
        Assert.Equal(100m, result.CostUsd);
        Assert.Equal(18.50m, result.ExchangeRate);
        Assert.Equal("Move 3 servers", result.Description);
        Assert.Equal(ServiceRequestStatus.Pending, result.Status);
        _requests.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(ContractStatus.Expired)]
    [InlineData(ContractStatus.OnHold)]
    public async Task CreateAsync_BlockedContract_ThrowsAndNeverSaves(ContractStatus status)
    {
        GivenContract(status);
        GivenRate(18.50m);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateSut().CreateAsync(7, "Some work", 10m));

        _requests.Verify(r => r.AddAsync(It.IsAny<ServiceRequest>(), It.IsAny<CancellationToken>()), Times.Never);
        // The currency API should not even be called when the workflow blocks the request.
        _rates.Verify(r => r.GetUsdToZarRateAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ApiDown_ThrowsExternalServiceException()
    {
        GivenContract(ContractStatus.Active);
        _rates.Setup(r => r.GetUsdToZarRateAsync(It.IsAny<CancellationToken>()))
              .ReturnsAsync(ExchangeRateResult.Fail("Service unavailable"));

        var ex = await Assert.ThrowsAsync<ExternalServiceException>(() => CreateSut().CreateAsync(7, "Some work", 10m));

        Assert.Equal("Service unavailable", ex.Message);
        _requests.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_UnknownContract_ThrowsNotFound()
    {
        _contracts.Setup(r => r.GetByIdAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                  .ReturnsAsync((Contract?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().CreateAsync(99, "Some work", 10m));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_MissingDescription_Throws(string? description)
    {
        GivenContract(ContractStatus.Active);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateSut().CreateAsync(7, description, 10m));
    }

    [Fact]
    public async Task CreateAsync_NegativeCost_Throws()
    {
        GivenContract(ContractStatus.Active);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateSut().CreateAsync(7, "Some work", -5m));
    }

    [Fact]
    public async Task CreateAsync_ZeroCost_IsAllowedAndCostsZeroRand()
    {
        GivenContract(ContractStatus.Draft);
        GivenRate(18.50m);

        var result = await CreateSut().CreateAsync(7, "Free warranty call-out", 0m);

        Assert.Equal(0m, result.Cost);
    }

    [Fact]
    public async Task UpdateStatusAsync_CompletedRequest_CannotChange()
    {
        _requests.Setup(r => r.GetByIdAsync(1, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new ServiceRequest { Id = 1, Status = ServiceRequestStatus.Completed });

        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            CreateSut().UpdateStatusAsync(1, ServiceRequestStatus.InProgress));
    }
}
