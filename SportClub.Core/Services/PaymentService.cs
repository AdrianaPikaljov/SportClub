using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;

namespace SportClub.Core.Services;

/// <summary>
/// Simuleeritud kaardimakse. Kontrollib kaardi numbrit (Luhni algoritm), kehtivust ja CVC-d.
/// Päris raha ei liigu ja kaardi andmeid ei salvestata ega logita – tagastatakse ainult tüüp ja 4 viimast numbrit.
/// Testkaart 4000 0000 0000 0002 lükatakse alati tagasi (et saaks näidata ebaõnnestunud makset).
/// </summary>
public class SimulatedPaymentService : IPaymentService
{
    public const string DeclinedTestCard = "4000000000000002";

    private readonly IClock _clock;

    public SimulatedPaymentService(IClock clock) => _clock = clock;

    public PaymentResult Charge(CardDetails card, decimal amount)
    {
        var number = new string(card.Number.Where(char.IsDigit).ToArray());

        if (number.Length is < 12 or > 19 || !LuhnValid(number))
            throw new BusinessRuleException("Vigane kaardi number.", "Vigane kaart");

        if (!TryParseExpiry(card.Expiry, out var year, out var month))
            throw new BusinessRuleException("Kehtivus peab olema kujul KK/AA, nt 12/28.", "Vigane kaart");
        var now = _clock.Now;
        if (year < now.Year || (year == now.Year && month < now.Month))
            throw new BusinessRuleException("Kaart on aegunud.", "Vigane kaart");

        if (card.Cvc.Length is < 3 or > 4 || !card.Cvc.All(char.IsDigit))
            throw new BusinessRuleException("CVC peab olema 3–4 numbrit.", "Vigane kaart");

        if (amount <= 0)
            throw new BusinessRuleException("Summa peab olema suurem kui 0.");

        if (number == DeclinedTestCard)
            throw new PaymentDeclinedException("Pank lükkas makse tagasi. Proovi teist kaarti.");

        return new PaymentResult($"SIM-{Guid.NewGuid():N}"[..16].ToUpperInvariant(), Brand(number), number[^4..]);
    }

    /// <summary>Luhni kontrollsumma – sama, mida kasutavad päris kaardid.</summary>
    public static bool LuhnValid(string digits)
    {
        var sum = 0;
        var doubleIt = false;
        for (var i = digits.Length - 1; i >= 0; i--)
        {
            var d = digits[i] - '0';
            if (doubleIt)
            {
                d *= 2;
                if (d > 9) d -= 9;
            }
            sum += d;
            doubleIt = !doubleIt;
        }
        return sum % 10 == 0;
    }

    private static bool TryParseExpiry(string text, out int year, out int month)
    {
        year = month = 0;
        var parts = text.Split('/');
        if (parts.Length != 2 || !int.TryParse(parts[0], out month) || !int.TryParse(parts[1], out var yy)) return false;
        if (month is < 1 or > 12) return false;
        year = 2000 + yy;
        return true;
    }

    private static string Brand(string number) => number[0] switch
    {
        '4' => "Visa",
        '5' or '2' => "Mastercard",
        '3' => "Amex",
        _ => "Kaart"
    };
}
