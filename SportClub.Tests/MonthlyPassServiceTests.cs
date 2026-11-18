using Moq;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;
using SportClub.Core.Services;

namespace SportClub.Tests;

/// <summary>Buying a monthly pass from a plan.</summary>
public class MonthlyPassServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 10, 0, 0, DateTimeKind.Utc);

    private readonly Mock<IMonthlyPassRepository> _passes = new();
    private readonly Mock<IPassPlanRepository> _plans = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IClock> _clock = new();
    private readonly PassPlan _plan = new() { Id = 3, Name = "Kuukaart 12", Visits = 12, DurationDays = 30, Price = 49m, IsActive = true };
    private readonly CurrentUser _client = new(5, Roles.Client);

    public MonthlyPassServiceTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _plans.Setup(r => r.GetByIdAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(_plan);
        _users.Setup(r => r.GetByIdAsync(5, It.IsAny<CancellationToken>()))
              .ReturnsAsync(new User { Id = 5, FullName = "Mari Tamm", Role = Roles.Client });
    }

    private MonthlyPassService CreateService() =>
        new(_passes.Object, _plans.Object, _users.Object, _notifications.Object, _uow.Object, _clock.Object);

    [Fact]
    public async Task Purchase_WithoutCurrentPass_StartsTodayWithPlanVisitsAndPrice()
    {
        var result = await CreateService().PurchaseAsync(3, _client);

        Assert.Equal(Now, result.ValidFrom);
        Assert.Equal(Now.AddDays(30), result.ValidUntil);
        Assert.Equal(12, result.VisitsTotal);
        Assert.Equal(49m, result.PricePaid);
        Assert.True(result.IsActive);
        _notifications.Verify(n => n.AddAsync(It.Is<Notification>(x => x.Type == "PassPurchased"), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Purchase_WhenCurrentPassHasVisits_StartsAfterCurrentEnds()
    {
        var current = new MonthlyPass { ValidFrom = Now.AddDays(-10), ValidUntil = Now.AddDays(20), VisitsTotal = 12, VisitsUsed = 5 };
        _passes.Setup(r => r.GetLatestEndingAsync(5, Now, It.IsAny<CancellationToken>())).ReturnsAsync(current);

        var result = await CreateService().PurchaseAsync(3, _client);

        Assert.Equal(current.ValidUntil.AddSeconds(1), result.ValidFrom);
        Assert.True(result.IsUpcoming);
    }

    [Fact]
    public async Task Purchase_WhenNextPassAlreadyWaiting_ThrowsConflict()
    {
        _passes.Setup(r => r.HasUpcomingAsync(5, Now, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        await Assert.ThrowsAsync<ConflictException>(() => CreateService().PurchaseAsync(3, _client));
        _passes.Verify(r => r.AddAsync(It.IsAny<MonthlyPass>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Purchase_InactivePlan_ThrowsBusinessRule()
    {
        _plan.IsActive = false;

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().PurchaseAsync(3, _client));
    }
}
