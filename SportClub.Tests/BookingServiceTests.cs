using Moq;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;
using SportClub.Core.Services;

namespace SportClub.Tests;

/// <summary>Broneerimise reeglid. Repositoryd on asendatud Moq mockidega.</summary>
public class BookingServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 26, 10, 0, 0);

    private readonly Mock<ISessionRepository> _sessions = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<IMonthlyPassRepository> _passes = new();
    private readonly Mock<ITrainerRepository> _trainers = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISessionNotifier> _notifier = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IEmailSender> _email = new();

    private readonly Session _session = new()
    {
        Id = 10,
        TrainerId = 2,
        StartsAt = Now.AddDays(1),
        Capacity = 3,
        TrainingType = new TrainingType { Id = 5, Name = "Crossfit" },
        Trainer = new Trainer { Id = 2, FullName = "Martin Kask" }
    };

    private readonly MonthlyPass _pass = new()
    {
        Id = 1, UserId = 5, ValidFrom = Now.AddDays(-10), ValidUntil = Now.AddDays(20), VisitsTotal = 8, VisitsUsed = 2
    };

    private readonly CurrentUser _client = new(5, Roles.Client);

    public BookingServiceTests()
    {
        _clock.Setup(c => c.Now).Returns(Now);
        _sessions.Setup(r => r.GetDetailedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(_session);
        _passes.Setup(r => r.GetUsableAsync(5, _session.StartsAt, It.IsAny<CancellationToken>())).ReturnsAsync(_pass);
        _bookings.Setup(r => r.GetWaitlistAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Booking>());
    }

    private BookingService CreateService() => new(_sessions.Object, _bookings.Object, _passes.Object, _trainers.Object,
        _uow.Object, _notifier.Object, _clock.Object, _users.Object, _email.Object);

    private void SetBookedCount(int count) =>
        _sessions.Setup(r => r.CountBookedAsync(10, It.IsAny<CancellationToken>())).ReturnsAsync(count);

    [Fact]
    public async Task Book_WhenSpotIsFree_CreatesBookingAndUsesOneVisit()
    {
        SetBookedCount(1);

        var result = await CreateService().BookAsync(10, _client);

        Assert.Equal("Booked", result.Status);
        Assert.Null(result.Position);
        Assert.Equal(3, _pass.VisitsUsed);
        _bookings.Verify(r => r.AddAsync(It.Is<Booking>(b => b.UserId == 5 && b.MonthlyPassId == 1 && b.Status == BookingStatus.Booked),
            It.IsAny<CancellationToken>()), Times.Once);
        // 3 kohta, enne 1 broneeritud, nüüd 2 → 1 vaba
        _notifier.Verify(n => n.FreeSpotsChangedAsync(10, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Book_WhenSessionIsFull_AddsToWaitlistWithoutUsingVisit()
    {
        SetBookedCount(3);
        _bookings.Setup(r => r.GetWaitlistAsync(10, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(() => new List<Booking> { new() { Id = 0, SessionId = 10, Status = BookingStatus.Waitlisted } });

        var result = await CreateService().BookAsync(10, _client);

        Assert.Equal("Waitlisted", result.Status);
        Assert.Equal(1, result.Position);
        Assert.Equal(2, _pass.VisitsUsed); // külastust ei võetud
        _bookings.Verify(r => r.AddAsync(It.Is<Booking>(b => b.Status == BookingStatus.Waitlisted && b.MonthlyPassId == null),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Book_WhenAlreadyBooked_ThrowsConflict()
    {
        _bookings.Setup(r => r.GetForUserAsync(10, 5, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(new Booking { Id = 3, SessionId = 10, UserId = 5, Status = BookingStatus.Booked });

        await Assert.ThrowsAsync<ConflictException>(() => CreateService().BookAsync(10, _client));
        _bookings.Verify(r => r.AddAsync(It.IsAny<Booking>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Book_WithoutPass_ThrowsBusinessRule()
    {
        SetBookedCount(0);
        _passes.Setup(r => r.GetUsableAsync(5, _session.StartsAt, It.IsAny<CancellationToken>())).ReturnsAsync((MonthlyPass?)null);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().BookAsync(10, _client));
    }

    [Fact]
    public async Task Book_PastSession_ThrowsBusinessRule()
    {
        _session.StartsAt = Now.AddHours(-1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().BookAsync(10, _client));
    }

    [Fact]
    public async Task Cancel_MoreThanTwoHoursBefore_GivesVisitBackAndDeletes()
    {
        var booking = new Booking { Id = 7, SessionId = 10, Session = _session, UserId = 5, Status = BookingStatus.Booked, MonthlyPass = _pass };
        _bookings.Setup(r => r.GetDetailedAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(booking);
        SetBookedCount(2);

        await CreateService().CancelAsync(7, _client);

        Assert.Equal(1, _pass.VisitsUsed);
        _bookings.Verify(r => r.Remove(booking), Times.Once);
        _notifier.Verify(n => n.FreeSpotsChangedAsync(10, 1, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Cancel_LessThanTwoHoursBefore_ThrowsAndKeepsVisit()
    {
        _session.StartsAt = Now.AddMinutes(90);
        var booking = new Booking { Id = 7, SessionId = 10, Session = _session, UserId = 5, Status = BookingStatus.Booked, MonthlyPass = _pass };
        _bookings.Setup(r => r.GetDetailedAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        await Assert.ThrowsAsync<BusinessRuleException>(() => CreateService().CancelAsync(7, _client));
        Assert.Equal(2, _pass.VisitsUsed);
        _bookings.Verify(r => r.Remove(It.IsAny<Booking>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_WaitlistedLessThanTwoHoursBefore_IsAllowed()
    {
        _session.StartsAt = Now.AddMinutes(90);
        var entry = new Booking { Id = 8, SessionId = 10, Session = _session, UserId = 5, Status = BookingStatus.Waitlisted };
        _bookings.Setup(r => r.GetDetailedAsync(8, It.IsAny<CancellationToken>())).ReturnsAsync(entry);

        await CreateService().CancelAsync(8, _client);

        _bookings.Verify(r => r.Remove(entry), Times.Once);
        Assert.Equal(2, _pass.VisitsUsed);
    }

    [Fact]
    public async Task Cancel_SomeoneElsesBooking_ThrowsForbidden()
    {
        var booking = new Booking { Id = 9, SessionId = 10, Session = _session, UserId = 99, Status = BookingStatus.Booked };
        _bookings.Setup(r => r.GetDetailedAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync(booking);

        await Assert.ThrowsAsync<ForbiddenException>(() => CreateService().CancelAsync(9, _client));
    }
}
