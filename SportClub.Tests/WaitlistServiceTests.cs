using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using SportClub.Core.Dtos;
using SportClub.Core.Entities;
using SportClub.Core.Exceptions;
using SportClub.Core.Interfaces;
using SportClub.Core.Services;
using System.ComponentModel.DataAnnotations;

namespace SportClub.Tests;

public class WaitlistServiceTests
{
    private static readonly DateTime Now = new(2026, 11, 16, 8, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ISessionRepository> _sessions = new();
    private readonly Mock<IBookingRepository> _bookings = new();
    private readonly Mock<IWaitlistRepository> _waitlist = new();
    private readonly Mock<IMonthlyPassRepository> _passes = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<ISessionNotifier> _notifier = new();
    private readonly Mock<IClock> _clock = new();

    private readonly Session _session = new()
    {
        Id = 20, StartsAt = Now.AddDays(1), MaxParticipants = 2, Room = "Rattasaal",
        TrainingType = new TrainingType { Id = 3, Name = "Spinning" }
    };

    public WaitlistServiceTests()
    {
        _clock.Setup(c => c.UtcNow).Returns(Now);
        _sessions.Setup(r => r.GetDetailedAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(_session);
    }

    private WaitlistService CreateService() => new(_sessions.Object, _bookings.Object, _waitlist.Object, _passes.Object,
        _notifications.Object, _uow.Object, _notifier.Object, _clock.Object, NullLogger<WaitlistService>.Instance);

    private static MonthlyPass PassFor(int userId) =>
        new() { Id = userId * 10, UserId = userId, ValidFrom = Now.AddDays(-1), ValidUntil = Now.AddDays(30), VisitsTotal = 12 };

    [Fact]
    public async Task Join_WhenSessionHasFreeSpots_ThrowsConflict()
    {
        _sessions.Setup(r => r.CountActiveBookingsAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(1);

        var ex = await Assert.ThrowsAsync<ConflictException>(() => CreateService().JoinAsync(20, new CurrentUser(5, Roles.Client)));
        Assert.Equal("Vabu kohti on", ex.Title);
    }

    [Fact]
    public async Task Join_WhenSessionIsFull_AddsEntryWithPosition()
    {
        _sessions.Setup(r => r.CountActiveBookingsAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(2);
        _passes.Setup(r => r.GetUsableAsync(5, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(PassFor(5));
        WaitlistEntry? added = null;
        _waitlist.Setup(r => r.AddAsync(It.IsAny<WaitlistEntry>(), It.IsAny<CancellationToken>()))
                 .Callback<WaitlistEntry, CancellationToken>((e, _) => { e.Id = 77; added = e; });
        _waitlist.Setup(r => r.GetOrderedForSessionAsync(20, It.IsAny<CancellationToken>()))
                 .ReturnsAsync(() => new List<WaitlistEntry> { new() { Id = 70 }, added! });

        var result = await CreateService().JoinAsync(20, new CurrentUser(5, Roles.Client));

        Assert.Equal(2, result.Position);
        Assert.Equal(77, result.Id);
    }

    [Fact]
    public async Task Promote_GivesFreedSpotToFirstInQueue_FifoOrder()
    {
        _sessions.Setup(r => r.GetUpcomingWithWaitlistAsync(Now, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Session> { _session });
        _sessions.Setup(r => r.CountActiveBookingsAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(1); // 1 spot free
        var first = new WaitlistEntry { Id = 1, SessionId = 20, UserId = 5, CreatedAt = Now.AddHours(-2) };
        var second = new WaitlistEntry { Id = 2, SessionId = 20, UserId = 6, CreatedAt = Now.AddHours(-1) };
        _waitlist.Setup(r => r.GetOrderedForSessionAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(new List<WaitlistEntry> { first, second });
        var pass = PassFor(5);
        _passes.Setup(r => r.GetUsableAsync(5, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(pass);

        var result = await CreateService().PromoteAsync();

        Assert.Equal(new PromoteResultDto(1, 0), result);
        Assert.Equal(1, pass.VisitsUsed);
        _bookings.Verify(r => r.AddAsync(It.Is<Booking>(b => b.UserId == 5 && b.SessionId == 20), It.IsAny<CancellationToken>()), Times.Once);
        _bookings.Verify(r => r.AddAsync(It.Is<Booking>(b => b.UserId == 6), It.IsAny<CancellationToken>()), Times.Never);
        _waitlist.Verify(r => r.Remove(first), Times.Once);
        _waitlist.Verify(r => r.Remove(second), Times.Never);
        _notifier.Verify(n => n.FreeSpotsChangedAsync(20, 0, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Promote_SkipsUserWithoutPassAndTakesNext()
    {
        _sessions.Setup(r => r.GetUpcomingWithWaitlistAsync(Now, It.IsAny<CancellationToken>())).ReturnsAsync(new List<Session> { _session });
        _sessions.Setup(r => r.CountActiveBookingsAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var noPass = new WaitlistEntry { Id = 1, SessionId = 20, UserId = 5 };
        var hasPass = new WaitlistEntry { Id = 2, SessionId = 20, UserId = 6 };
        _waitlist.Setup(r => r.GetOrderedForSessionAsync(20, It.IsAny<CancellationToken>())).ReturnsAsync(new List<WaitlistEntry> { noPass, hasPass });
        _passes.Setup(r => r.GetUsableAsync(6, It.IsAny<DateTime>(), It.IsAny<CancellationToken>())).ReturnsAsync(PassFor(6));

        var result = await CreateService().PromoteAsync();

        Assert.Equal(new PromoteResultDto(1, 1), result);
        _bookings.Verify(r => r.AddAsync(It.Is<Booking>(b => b.UserId == 6), It.IsAny<CancellationToken>()), Times.Once);
    }
}

/// <summary>Tests without mocks: entity and DTO rules.</summary>
public class ModelRuleTests
{
    [Fact]
    public void MonthlyPass_VisitsLeftAndValidity()
    {
        var pass = new MonthlyPass
        {
            ValidFrom = new DateTime(2026, 11, 1, 0, 0, 0, DateTimeKind.Utc),
            ValidUntil = new DateTime(2026, 11, 30, 23, 59, 59, DateTimeKind.Utc),
            VisitsTotal = 12,
            VisitsUsed = 5
        };

        Assert.Equal(7, pass.VisitsLeft);
        Assert.True(pass.IsValidAt(new DateTime(2026, 11, 15, 0, 0, 0, DateTimeKind.Utc)));
        Assert.False(pass.IsValidAt(new DateTime(2026, 12, 1, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void SessionSaveDto_InPast_IsInvalid()
    {
        var dto = new SessionSaveDto
        {
            TrainingTypeId = 1, TrainerId = 1, StartsAt = DateTime.UtcNow.AddDays(-1), Room = "Saal 1", MaxParticipants = 10
        };
        var results = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(results, r => r.MemberNames.Contains(nameof(SessionSaveDto.StartsAt)));
    }

    [Fact]
    public void MonthlyPassCreateDto_EndBeforeStart_IsInvalid()
    {
        var dto = new MonthlyPassCreateDto
        {
            UserId = 1, ValidFrom = new DateTime(2026, 12, 1), ValidUntil = new DateTime(2026, 11, 1), VisitsTotal = 12
        };
        var results = new List<ValidationResult>();

        Assert.False(Validator.TryValidateObject(dto, new ValidationContext(dto), results, true));
    }
}
