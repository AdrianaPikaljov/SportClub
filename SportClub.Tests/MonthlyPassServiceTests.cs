using Moq;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;
using SportClub.Core.Services;

namespace SportClub.Tests;

/// <summary>Kuukaardi ostmine.</summary>
public class MonthlyPassServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 10, 0, 0);

    private readonly Mock<IMonthlyPassRepository> _passes = new();
    private readonly Mock<IPassPlanRepository> _plans = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IPaymentService> _payments = new();
    private readonly Mock<IEmailSender> _email = new();
    private static readonly CardDetails Card = new("4242 4242 4242 4242", "Mari Maasikas", "12/30", "123");
    private readonly PassPlan _plan = new() { Id = 2, Name = "8 külastust", Visits = 8, Days = 30, Price = 39m, IsActive = true };
    private readonly CurrentUser _client = new(5, Roles.Client);

    public MonthlyPassServiceTests()
    {
        _clock.Setup(c => c.Now).Returns(Now);
        _plans.Setup(r => r.GetByIdAsync(2, It.IsAny<CancellationToken>())).ReturnsAsync(_plan);
        _payments.Setup(p => p.Charge(It.IsAny<CardDetails>(), It.IsAny<decimal>()))
                 .Returns(new PaymentResult("SIM-TEST", "Visa", "4242"));
    }

    private MonthlyPassService CreateService() => new(_passes.Object, _plans.Object, _uow.Object, _clock.Object,
        _users.Object, _payments.Object, _email.Object);

    [Fact]
    public async Task Buy_WithoutActivePass_StartsTodayWithPlanVisits()
    {
        var result = await CreateService().BuyAsync(2, Card, _client);

        Assert.Equal("8 külastust", result.Plan);
        Assert.Equal(8, result.VisitsLeft);
        Assert.Equal("2026-11-26", result.ValidFrom);
        Assert.Equal("2026-12-26", result.ValidUntil);
        _passes.Verify(r => r.AddAsync(It.Is<MonthlyPass>(p => p.UserId == 5 && p.PricePaid == 39m), It.IsAny<CancellationToken>()), Times.Once);
        // Kaardist jääb alles ainult tüüp ja 4 viimast numbrit
        Assert.Equal("Visa •••• 4242", result.Card);
        _payments.Verify(p => p.Charge(Card, 39m), Times.Once);
    }

    [Fact]
    public async Task Buy_WhenPaymentDeclined_DoesNotCreatePass()
    {
        _payments.Setup(p => p.Charge(It.IsAny<CardDetails>(), It.IsAny<decimal>()))
                 .Throws(new PaymentDeclinedException("Pank lükkas makse tagasi."));

        await Assert.ThrowsAsync<PaymentDeclinedException>(() => CreateService().BuyAsync(2, Card, _client));
        _passes.Verify(r => r.AddAsync(It.IsAny<MonthlyPass>(), It.IsAny<CancellationToken>()), Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Buy_WhenActivePassExists_ThrowsConflict()
    {
        _passes.Setup(r => r.GetUsableAsync(5, Now, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new MonthlyPass { Id = 1, UserId = 5, VisitsTotal = 8, VisitsUsed = 3 });

        await Assert.ThrowsAsync<ConflictException>(() => CreateService().BuyAsync(2, Card, _client));
        _passes.Verify(r => r.AddAsync(It.IsAny<MonthlyPass>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Buy_UnknownOrRemovedPlan_ThrowsNotFound()
    {
        _plan.IsActive = false;

        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().BuyAsync(2, Card, _client));
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().BuyAsync(99, Card, _client));
    }

    [Fact]
    public async Task GetMy_WithoutActivePass_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().GetMyAsync(5));
    }
}
