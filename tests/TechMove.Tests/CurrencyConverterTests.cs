using TechMove.Web.Services.Currency;

namespace TechMove.Tests;

public class CurrencyConverterTests
{
    private readonly CurrencyConverter _sut = new();

    [Theory]
    [InlineData(100, 18.50, 1850.00)]
    [InlineData(1, 18.2345, 18.23)]      // rounds down
    [InlineData(1, 18.2355, 18.24)]      // midpoint rounds away from zero
    [InlineData(250.75, 17.9, 4488.43)]  // 4488.425 -> 4488.43
    [InlineData(0.01, 18.5, 0.19)]       // 0.185 -> 0.19
    public void ConvertUsdToZar_GivenRate_ReturnsCorrectAmount(decimal usd, decimal rate, decimal expected)
    {
        var result = _sut.ConvertUsdToZar(usd, rate);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertUsdToZar_ZeroAmount_ReturnsZero()
    {
        Assert.Equal(0m, _sut.ConvertUsdToZar(0m, 18.5m));
    }

    [Fact]
    public void ConvertUsdToZar_LargeAmount_IsPreciseWithDecimal()
    {
        // 1,000,000.33 * 18.123456 = 18,123,461.980740... -> 18,123,461.98
        Assert.Equal(18_123_461.98m, _sut.ConvertUsdToZar(1_000_000.33m, 18.123456m));
    }

    [Fact]
    public void ConvertUsdToZar_NegativeAmount_Throws()
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _sut.ConvertUsdToZar(-1m, 18.5m));
        Assert.Equal("usdAmount", ex.ParamName);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-18.5)]
    public void ConvertUsdToZar_ZeroOrNegativeRate_Throws(decimal rate)
    {
        var ex = Assert.Throws<ArgumentOutOfRangeException>(() => _sut.ConvertUsdToZar(100m, rate));
        Assert.Equal("usdToZarRate", ex.ParamName);
    }
}
