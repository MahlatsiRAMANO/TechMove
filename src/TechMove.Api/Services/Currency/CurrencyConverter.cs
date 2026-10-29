namespace TechMove.Api.Services.Currency;

public interface ICurrencyConverter
{
    /// <summary>Converts a USD amount to ZAR using the given rate, rounded to cents.</summary>
    decimal ConvertUsdToZar(decimal usdAmount, decimal usdToZarRate);
}

/// <summary>
/// Pure, side-effect-free currency maths. Kept separate from the HTTP call so it can be
/// tested precisely with a fixed rate.
/// </summary>
public sealed class CurrencyConverter : ICurrencyConverter
{
    public decimal ConvertUsdToZar(decimal usdAmount, decimal usdToZarRate)
    {
        if (usdAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usdAmount), usdAmount, "Amount cannot be negative.");
        }

        if (usdToZarRate <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(usdToZarRate), usdToZarRate, "Exchange rate must be greater than zero.");
        }

        // decimal (not double) avoids binary floating-point errors with money.
        return Math.Round(usdAmount * usdToZarRate, 2, MidpointRounding.AwayFromZero);
    }
}
