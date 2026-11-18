using Moq;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;
using SportClub.Core.Services;

namespace SportClub.Tests;

/// <summary>Unit tests for booking rules. Repositories are replaced with Moq mocks.</summary>
public class BookingServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ISessionRepository> _sessions = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<IWaitlistRepository> _waitlist = new();
    private readonly Mock<IMonthlyPassRepository> _passes = new();
    private readonly Mock<ITrainerRepository> _trainers = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISessionNotifier> _notifier = new();
    private readonly Mock<IClock> _clock = new();

    private readonly Session _session = new()
    {
        Id = 10,
        TrainerId = 2,
        StartsAt = Now.AddDays(1),
        MaxParticipants = 3,
        Room = "Saal 1",
        TrainingType = new TrainingType { Id = 2, Name = "HIIT" },
        Trainer = new Trainer { Id = 2, FullName = "Martin Kask" }
    };

    private readonly MonthlyPass _pass = new()
    {
        Id = 1, UserId = 5, ValidFrom = Now.AddDays(-10), ValidUntil = Now.AddDays(20), VisitsTotal = 12, VisitsUsed = 4
    };

    private readonly CurrentUser _client = new(5, Roles.Client);

    public BookingServiceTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _sessions.Setup(r => r.GetDetailedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(_session);
        _passes.Setup(r => r.GetUsableAsync(5, _session.StartsAt, It.IsAny<CancellationToken>())).ReturnsAsync(_pass);
    }

    private BookingService CreateService() => new(_sessions.Object, _bookings.Object, _waitlist.Object, _passes.Object,
        _trainers.Object, _uow.Object, _notifier.Object, _clock.Object);

    private void SetBookedCount(int count) =>
        _sessions.Setup(r => r.CountActiveBookingsAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(count);

    [Fact]
    public async Task Book_WhenSpotIsFree_CreatesBookingAndUsesOneVisit()
    {
        SetBookedCount(1);

        var result = await CreateService().BookAsync(10, _client);

        Assert.Equal(10, result.SessionId);
        Assert.Equal("Active", result.Status);
        Assert.Equal(5, _pass.VisitsUsed);
        _bookings.Verify(r => r.AddAsync(It.Is<Booking>(b => b.UserId == 5 && b.MonthlyPassId == 1), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Book_WhenSpotIsFree_SendsFreeSpotsChangedWithNewCount()
    {
        SetBookedCount(1);

        await CreateService().BookAsync(10, _client);

        // 3 places, 1 booked before, now 2 booked → 1 free
        _notifier.Verify(n => n.FreeSpotsChangedAsync(10, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Book_WhenSessionIsFull_ThrowsSessionFullException()
    {
        SetBookedCount(3);

        var ex = await Assert.ThrowsAsync<SessionFullException>(() => CreateService().BookAsync(10, _client));

        Assert.Equal(409, ex.StatusCode);
        _bookings.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
        Assert.Equal(4, _pass.VisitsUsed);
    }

    [Fact]
    public async Task Book_WhenAlreadyBooked_ThrowsConflict()
    {
        SetBookedCount(1);
        _bookings.Setup(r => r.GetActiveAsync(10, 5, It.IsAny<CancellationToken>())).ReturnsAsync(new Booking { Id = 1 });

        await Assert.ThrowsAsync<ConflictException>(() => CreateService().BookAsync(10, _client));
    }

    [Fact]
    public async Task Book_WithoutUsablePass_ThrowsBusinessRule()
    {
        SetBookedCount(0);
        _passes.Setup(r => r.GetUsableAsync(5, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync((MonthlyPass?)null);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().BookAsync(10, _client));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Book_WhenSessionAlreadyStarted_ThrowsBusinessRule()
    {
        _session.StartsAt = Now.AddMinutes(-5);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().BookAsync(10, _client));
    }

    [Fact]
    public async Task Book_UnknownSession_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => CreateService().BookAsync(999, _client));
    }

    [Fact]
    public async Task Book_RemovesUserFromWaitlistOfSameSession()
    {
        SetBookedCount(2);
        var entry = new WaitlistEntry { Id = 7, SessionId = 10, UserId = 5 };
        _waitlist.Setup(r => r.GetAsync(10, 5, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

        await CreateService().BookAsync(10, _client);

        _waitlist.Verify(r => r.Remove(entry), Times.Once);
    }

    [Fact]
    public async Task Cancel_ReturnsVisitToPassAndNotifies()
    {
        var booking = new Booking { Id = 3, SessionId = 10, Session = _session, UserId = 5, MonthlyPass = _pass };
        _bookings.Setup(r => r.GetWithSessionAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        SetBookedCount(2);

        await CreateService().CancelAsync(3, _client);

        Assert.Equal(BookingStatus.Cancelled, booking.Status);
        Assert.Equal(3, _pass.VisitsUsed);
        _notifier.Verify(n => n.FreeSpotsChangedAsync(10, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_SomeoneElsesBooking_ThrowsForbidden()
    {
        var booking = new Booking { Id = 3, SessionId = 10, Session = _session, UserId = 99 };
        _bookings.Setup(r => r.GetWithSessionAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService().CancelAsync(3, _client));
        Assert.Equal(BookingStatus.Active, booking.Status);
    }

    [Fact]
    public async Task Attendance_ByTrainerOfAnotherSession_ThrowsForbidden()
    {
        _session.StartsAt = Now.AddMinutes(10);
        var booking = new Booking { Id = 3, SessionId = 10, Session = _session, UserId = 5 };
        _bookings.Setup(r => r.GetWithSessionAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _trainers.Setup(r => r.GetByUserIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(new Trainer { Id = 1 });

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            CreateService().SetAttendanceAsync(3, true, new CurrentUser(42, Roles.Trainer)));
    }

    [Fact]
    public async Task Attendance_ByOwnTrainer_SavesValue()
    {
        _session.StartsAt = Now.AddMinutes(10);
        var booking = new Booking { Id = 3, SessionId = 10, Session = _session, UserId = 5 };
        _bookings.Setup(r => r.GetWithSessionAsync(3, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        _trainers.Setup(r => r.GetByUserIdAsync(42, It.IsAny<CancellationToken>())).ReturnsAsync(new Trainer { Id = 2 });

        var result = await CreateService().SetAttendanceAsync(3, true, new CurrentUser(42, Roles.Trainer));

        Assert.True(result.Attended);
    }
}
