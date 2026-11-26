using Moq;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;
using SportClub.Core.Services;
using Xunit;

namespace SportClub.Tests;

/// <summary>Simuleeritud kaardimakse: Luhni kontroll, kehtivus, CVC ja tagasilükatud testkaart.</summary>
public class PaymentServiceTests
{
    private readonly SimulatedPaymentService _service;

    public PaymentServiceTests()
    {
        var clock = new Mock<IClock>();
        clock.Setup(c => c.Now).Returns(new DateTime(2026, 11, 26, 10, 0, 0));
        _service = new SimulatedPaymentService(clock.Object);
    }

    [Fact]
    public void Charge_ValidCard_ReturnsOnlyBrandAndLast4()
    {
        var result = _service.Charge(new CardDetails("4242 4242 4242 4242", "Mari Maasikas", "12/28", "123"), 39m);

        Assert.Equal("Visa", result.Brand);
        Assert.Equal("4242", result.Last4);
        Assert.StartsWith("SIM-", result.Reference);
    }

    [Fact]
    public void Charge_DeclinedTestCard_ThrowsPaymentDeclined()
    {
        Assert.Throws<PaymentDeclinedException>(() =>
            _service.Charge(new CardDetails("4000 0000 0000 0002", "Mari Maasikas", "12/28", "123"), 39m));
    }

    [Theory]
    [InlineData("4242 4242 4242 4241", "12/28", "123")] // Luhn ei klapi
    [InlineData("4242 4242 4242 4242", "10/26", "123")] // aegunud
    [InlineData("4242 4242 4242 4242", "13/28", "123")] // vale kuu
    [InlineData("4242 4242 4242 4242", "12/28", "12")]  // liiga lühike CVC
    public void Charge_InvalidCard_ThrowsBusinessRule(string number, string expiry, string cvc)
    {
        Assert.Throws<BusinessRuleException>(() =>
            _service.Charge(new CardDetails(number, "Mari Maasikas", expiry, cvc), 39m));
    }
}
