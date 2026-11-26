using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SportClub.Core.Entities;
using SportClub.Core.Interfaces;
using SportClub.Core.Services;

namespace SportClub.Tests;

/// <summary>Ootejärjekorrast kohale viimine (POST /api/waitlist/promote).</summary>
public class WaitlistServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 10, 0, 0);

    private readonly Mock<ISessionRepository> _sessions = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<IMonthlyPassRepository> _passes = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISessionNotifier> _notifier = new();
    private readonly Mock<IClock> _clock = new();

    private readonly Session _session = new()
    {
        Id = 10, StartsAt = Now.AddDays(1), Capacity = 3, TrainingType = new TrainingType { Name = "Crossfit" }
    };

    private readonly Booking _first;
    private readonly Booking _second;

    public WaitlistServiceTests()
    {
        _clock.Setup(c => c.Now).Returns(Now);
        _sessions.Setup(r => r.GetWithWaitlistAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new List<Session> { _session });

        _first = new Booking { Id = 21, SessionId = 10, UserId = 7, Status = BookingStatus.Waitlisted, CreatedAt = Now.AddHours(-3),
            User = new User { Id = 7, FullName = "Liis Saar" } };
        _second = new Booking { Id = 22, SessionId = 10, UserId = 8, Status = BookingStatus.Waitlisted, CreatedAt = Now.AddHours(-2),
            User = new User { Id = 8, FullName = "Peeter Mets" } };
        _bookings.Setup(r => r.GetWaitlistAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Booking> { _first, _second });
    }

    private WaitlistService CreateService() => new(_sessions.Object, _bookings.Object, _passes.Object, _notifications.Object,
        _uow.Object, _notifier.Object, _clock.Object, NullLogger<WaitlistService>.Instance);

    private void SetBookedCount(int count) =>
        _sessions.Setup(r => r.CountBookedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(count);

    [Fact]
    public async Task Promote_WhenOneSpotIsFree_MovesOnlyTheFirstAndUsesVisit()
    {
        SetBookedCount(2);
        var pass = new MonthlyPass { Id = 3, UserId = 7, VisitsTotal = 8, VisitsUsed = 1 };
        _passes.Setup(r => r.GetUsableAsync(7, _session.StartsAt, It.IsAny<CancellationToken>())).ReturnsAsync(pass);

        var result = await CreateService().PromoteAsync();

        Assert.Single(result.Promoted);
        Assert.Equal(21, result.Promoted[0].BookingId);
        Assert.Equal(BookingStatus.Booked, _first.Status);
        Assert.Equal(BookingStatus.Waitlisted, _second.Status);
        Assert.Equal(2, pass.VisitsUsed);
        _notifier.Verify(n => n.FreeSpotsChangedAsync(10, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Promote_WhenFirstHasNoPass_RemovesHimAndTakesNext()
    {
        SetBookedCount(2);
        _passes.Setup(r => r.GetUsableAsync(7, _session.StartsAt, It.IsAny<CancellationToken>())).ReturnsAsync((MonthlyPass?)null);
        _passes.Setup(r => r.GetUsableAsync(8, _session.StartsAt, It.IsAny<CancellationToken>()))
               .ReturnsAsync(new MonthlyPass { Id = 4, UserId = 8, VisitsTotal = 8 });

        var result = await CreateService().PromoteAsync();

        _bookings.Verify(r => r.Remove(_first), Times.Once);
        Assert.Equal(1, result.Removed);
        Assert.Equal(22, Assert.Single(result.Promoted).BookingId);
        Assert.Equal(BookingStatus.Booked, _second.Status);
    }

    [Fact]
    public async Task Promote_WhenSessionStartsWithinHour_RemovesWholeQueue()
    {
        _session.StartsAt = Now.AddMinutes(45);
        SetBookedCount(1);

        var result = await CreateService().PromoteAsync();

        Assert.Empty(result.Promoted);
        Assert.Equal(2, result.Removed);
        _bookings.Verify(r => r.Remove(It.IsAny<Booking>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Promote_WhenSessionIsFull_DoesNothing()
    {
        SetBookedCount(3);

        var result = await CreateService().PromoteAsync();

        Assert.Empty(result.Promoted);
        Assert.Equal(0, result.Removed);
        _passes.Verify(r => r.GetUsableAsync(It.IsAny<int>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
