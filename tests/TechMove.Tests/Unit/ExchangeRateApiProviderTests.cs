using System.Net;
using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using TechMove.Api.Services.Currency;

namespace TechMove.Tests;

/// <summary>Tests the HttpClient integration with a fake handler - no real network calls.</summary>
public class ExchangeRateApiProviderTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;
        public int Calls { get; private set; }

        public FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(_respond(request));
        }
    }

    private static ExchangeRateApiProvider CreateSut(FakeHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://example.test/") },
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<ExchangeRateApiProvider>.Instance,
            Options.Create(new ExchangeRateApiOptions()));

    private static HttpResponseMessage Json(string json, HttpStatusCode code = HttpStatusCode.OK) =>
        new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task GetRate_ValidResponse_ReturnsZarRate()
    {
        var handler = new FakeHandler(_ => Json("{\"result\":\"success\",\"base_code\":\"USD\",\"rates\":{\"USD\":1,\"ZAR\":18.2731}}"));

        var result = await CreateSut(handler).GetUsdToZarRateAsync();

        Assert.True(result.Success);
        Assert.Equal(18.2731m, result.Rate);
    }

    [Fact]
    public async Task GetRate_SecondCall_UsesCache()
    {
        var handler = new FakeHandler(_ => Json("{\"result\":\"success\",\"rates\":{\"ZAR\":18.0}}"));
        var sut = CreateSut(handler);

        await sut.GetUsdToZarRateAsync();
        await sut.GetUsdToZarRateAsync();

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task GetRate_ServerError_ReturnsFailureInsteadOfThrowing()
    {
        var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await CreateSut(handler).GetUsdToZarRateAsync();

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task GetRate_NetworkDown_ReturnsFailure()
    {
        var handler = new FakeHandler(_ => throw new HttpRequestException("No route to host"));

        var result = await CreateSut(handler).GetUsdToZarRateAsync();

        Assert.False(result.Success);
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("{\"result\":\"success\",\"rates\":{\"EUR\":0.9}}")]  // ZAR missing
    [InlineData("{\"result\":\"success\",\"rates\":{\"ZAR\":0}}")]    // zero rate
    [InlineData("{\"result\":\"error\",\"error-type\":\"quota-reached\"}")]
    public async Task GetRate_BadPayload_ReturnsFailure(string body)
    {
        var handler = new FakeHandler(_ => Json(body));

        var result = await CreateSut(handler).GetUsdToZarRateAsync();

        Assert.False(result.Success);
    }
}
