using SportClub.Core.Dtos;
using SportClub.Core.Entities;

namespace SportClub.Core.Interfaces;

/// <summary>Üldine repository lihtsa CRUD-i jaoks.</summary>
public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    void Remove(T entity);
}

/// <summary>Salvestab kõik muudatused korraga (DbContext).</summary>
public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken ct = default);
}

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<List<User>> SearchAsync(string? search, CancellationToken ct = default);
}

public interface ITrainerRepository : IRepository<Trainer>
{
    /// <summary>Kõik treenerid koos kontoga (e-post).</summary>
    Task<List<Trainer>> GetAllAsync(CancellationToken ct = default);
    Task<Trainer?> GetWithUserAsync(int id, CancellationToken ct = default);
    Task<Trainer?> GetByUserIdAsync(int userId, CancellationToken ct = default);
    Task<bool> HasSessionsAfterAsync(int trainerId, DateTime time, CancellationToken ct = default);
}

public interface ITrainingTypeRepository : IRepository<TrainingType>
{
    Task<List<TrainingType>> GetAllAsync(CancellationToken ct = default);
    Task<TrainingType?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<bool> NameExistsAsync(string name, int? exceptId, CancellationToken ct = default);
    Task<bool> HasSessionsAsync(int typeId, CancellationToken ct = default);
}

public interface ISessionRepository : IRepository<Session>
{
    /// <summary>Treening koos tüübi ja treeneriga.</summary>
    Task<Session?> GetDetailedAsync(int id, CancellationToken ct = default);
    /// <summary>Tunniplaan: from kaasa arvatud, to välja arvatud.</summary>
    Task<List<Session>> GetListAsync(DateTime? from, DateTime? to, int? typeId, int? trainerId, bool newestFirst, CancellationToken ct = default);
    /// <summary>Broneeritud, ootel olijate ja kohalolu märkimata inimeste arv iga treeningu kohta.</summary>
    Task<Dictionary<int, SessionCounts>> GetCountsAsync(IEnumerable<int> sessionIds, CancellationToken ct = default);
    Task<int> CountBookedAsync(int sessionId, CancellationToken ct = default);
    /// <summary>Kas treeneril on sel ajal juba teine treening.</summary>
    Task<bool> TrainerBusyAsync(int trainerId, DateTime start, DateTime end, int? exceptSessionId, CancellationToken ct = default);
    Task<List<Session>> GetStartingBetweenAsync(DateTime from, DateTime to, CancellationToken ct = default);
    /// <summary>Kõik treeningud, millel on keegi ootejärjekorras.</summary>
    Task<List<Session>> GetWithWaitlistAsync(CancellationToken ct = default);
    /// <summary>Kõik treeneri treeningud muutmiseks/kustutamiseks (jälgitavad, ilma seosteta).</summary>
    Task<List<Session>> GetAllForTrainerAsync(int trainerId, CancellationToken ct = default);
}

public interface IBookingRepository : IRepository<Booking>
{
    /// <summary>Broneering koos treeningu, kasutaja ja kuukaardiga.</summary>
    Task<Booking?> GetDetailedAsync(int id, CancellationToken ct = default);
    Task<Booking?> GetForUserAsync(int sessionId, int userId, CancellationToken ct = default);
    Task<List<Booking>> GetAllForUserAsync(int userId, CancellationToken ct = default);
    /// <summary>Kõik treeningu kirjed (Booked ja Waitlisted) koos kasutaja ja kuukaardiga.</summary>
    Task<List<Booking>> GetForSessionAsync(int sessionId, CancellationToken ct = default);
    /// <summary>Ootejärjekord õiges järjekorras (kes tuli varem, on eespool).</summary>
    Task<List<Booking>> GetWaitlistAsync(int sessionId, CancellationToken ct = default);
    Task<PagedResult<Booking>> SearchAsync(string? search, BookingStatus? status, BookingSort sort, int page, int pageSize, CancellationToken ct = default);
    Task<List<Booking>> GetWithoutReminderAsync(int sessionId, CancellationToken ct = default);
    /// <summary>Statistika jaoks: treeningud perioodis koos broneeritud ja kohal olnud inimeste arvuga.</summary>
    Task<List<PopularDto>> GetPopularAsync(DateTime? from, DateTime? to, int? trainerId, CancellationToken ct = default);
}

public interface IMonthlyPassRepository : IRepository<MonthlyPass>
{
    /// <summary>Kuukaart, mis kehtib antud ajal ja millel on külastusi alles.</summary>
    Task<MonthlyPass?> GetUsableAsync(int userId, DateTime time, CancellationToken ct = default);
    Task<List<MonthlyPass>> SearchAsync(string? search, CancellationToken ct = default);
}

public interface IPassPlanRepository : IRepository<PassPlan>
{
    Task<List<PassPlan>> GetActiveAsync(CancellationToken ct = default);
}

public interface INotificationRepository : IRepository<Notification>
{
}

/// <summary>
/// Saadab e-kirja. Teostus on API-s (Gmail SMTP). Viga kirja saatmisel ei tohi äriloogikat katki teha.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(string toEmail, string toName, string subject, string html, CancellationToken ct = default);
}

/// <summary>Maksekaardi andmed. Neid ei salvestata kuhugi – kasutatakse ainult makse kontrolliks.</summary>
public record CardDetails(string Number, string Holder, string Expiry, string Cvc);

/// <summary>Makse tulemus: salvestatakse ainult kaardi tüüp, 4 viimast numbrit ja makse viide.</summary>
public record PaymentResult(string Reference, string Brand, string Last4);

/// <summary>Makse (selles projektis simuleeritud, päris raha ei liigu).</summary>
public interface IPaymentService
{
    PaymentResult Charge(CardDetails card, decimal amount);
}

/// <summary>Treeningu loendurid: broneeritud, ootel, kohalolu märkimata.</summary>
public record SessionCounts(int Booked, int Waitlisted, int Unmarked);

/// <summary>Saadab reaalajas uuendused (API-s WebSocketi kaudu).</summary>
public interface ISessionNotifier
{
    Task FreeSpotsChangedAsync(int sessionId, int free, CancellationToken ct = default);
}

/// <summary>Kell. Testides asendatakse mockiga.</summary>
public interface IClock
{
    /// <summary>Praegune aeg klubi kohaliku aja järgi (Eesti aeg).</summary>
    DateTime Now { get; }
}

/// <summary>Eesti aeg (Europe/Tallinn), sõltumata serveri ajavööndist.</summary>
public class ClubClock : IClock
{
    private static readonly TimeZoneInfo Zone = FindZone();

    public DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Zone), DateTimeKind.Unspecified);

    private static TimeZoneInfo FindZone()
    {
        foreach (var id in new[] { "Europe/Tallinn", "FLE Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        return TimeZoneInfo.Local;
    }
}

/// <summary>Sisseloginud kasutaja (kontrollerist teenusele).</summary>
public record CurrentUser(int Id, string Role)
{
    public bool IsAdmin => Role == Roles.Admin;
    public bool IsTrainer => Role == Roles.Trainer;
}
