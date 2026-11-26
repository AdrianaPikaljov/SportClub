using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace SportClub.Core.Dtos;

// Väljapoole antakse ainult need klassid, mitte andmebaasi olemid.
// Kuupäevad on tekstina: "2026-11-27", aeg koos kellaajaga "2026-11-27 18:00" (Eesti aeg).

// ---------- Ühised ----------

/// <summary>Üks lehekülg tulemusi.</summary>
public record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total)
{
    public int TotalPages => PageSize == 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
}

// ---------- Sisselogimine ----------

public class RegisterRequest
{
    /// <example>Anna Kask</example>
    [Required(ErrorMessage = "Nimi on kohustuslik."), StringLength(100, MinimumLength = 2, ErrorMessage = "Nimi peab olema 2–100 tähemärki.")]
    public string Name { get; set; } = "";

    /// <example>anna@mail.ee</example>
    [Required(ErrorMessage = "E-post on kohustuslik."), EmailAddress(ErrorMessage = "Vigane e-posti aadress.")]
    public string Email { get; set; } = "";

    /// <example>Parool123</example>
    [Required(ErrorMessage = "Parool on kohustuslik."), StringLength(100, MinimumLength = 8, ErrorMessage = "Parool peab olema vähemalt 8 tähemärki.")]
    public string Password { get; set; } = "";
}

public class LoginRequest
{
    /// <example>klient@sportclub.ee</example>
    [Required(ErrorMessage = "E-post on kohustuslik."), EmailAddress(ErrorMessage = "Vigane e-posti aadress.")]
    public string Email { get; set; } = "";

    /// <example>Klient123!</example>
    [Required(ErrorMessage = "Parool on kohustuslik.")]
    public string Password { get; set; } = "";
}

public record LoginResponse(string Token, string Role, string Name);

public record UserDto(int Id, string Name, string Email, string Role);

// ---------- Tunniplaan ----------

/// <summary>Treening tunniplaanis. free = vabade kohtade arv.</summary>
public record SessionDto(int Id, string Type, int TrainerId, string Trainer, string StartsAt, int DurationMinutes, int Capacity, int Free);

/// <summary>Treeningu lisamine ja muutmine (admin). Sama kuju annab GET /api/sessions/{id}.</summary>
public class SessionSaveDto
{
    /// <summary>Treeningutüübi id (vt GET /api/types).</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue, ErrorMessage = "typeId on kohustuslik.")]
    public int TypeId { get; set; }

    /// <summary>Treeneri id (vt GET /api/trainers).</summary>
    /// <example>1</example>
    [Range(1, int.MaxValue, ErrorMessage = "trainerId on kohustuslik.")]
    public int TrainerId { get; set; }

    /// <summary>Kuupäev kujul AAAA-KK-PP.</summary>
    /// <example>2026-11-27</example>
    [Required(ErrorMessage = "Kuupäev on kohustuslik.")]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$", ErrorMessage = "Kuupäev peab olema kujul AAAA-KK-PP, nt 2026-11-27")]
    public string Date { get; set; } = "";

    /// <summary>Kellaaeg kujul TT:MM (Eesti aeg).</summary>
    /// <example>18:00</example>
    [Required(ErrorMessage = "Kellaaeg on kohustuslik.")]
    [RegularExpression(@"^\d{2}:\d{2}$", ErrorMessage = "Kellaaeg peab olema kujul TT:MM, nt 18:00")]
    public string Time { get; set; } = "";

    /// <example>60</example>
    [Range(15, 240, ErrorMessage = "Kestus peab olema 15–240 minutit.")]
    public int DurationMinutes { get; set; } = 60;

    /// <summary>Kohtade arv.</summary>
    /// <example>12</example>
    [Range(1, 200, ErrorMessage = "Kohtade arv peab olema 1–200.")]
    public int Capacity { get; set; } = 12;
}

// ---------- Treenerid ----------

/// <summary>Treener. email (konto e-post) näeb ainult admin, teistele seda välja ei anta.</summary>
public record TrainerDto(int Id, string Name, string Specialty, string? Description, string? Phone,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Email);

/// <summary>Treeneri andmed koos konto e-postiga. Sama kuju annab GET /api/trainers/{id}.</summary>
public class TrainerSaveDto
{
    /// <example>Mari Tamm</example>
    [Required(ErrorMessage = "Nimi on kohustuslik."), StringLength(100, MinimumLength = 2, ErrorMessage = "Nimi peab olema 2–100 tähemärki.")]
    public string Name { get; set; } = "";

    /// <example>Jooga</example>
    [Required(ErrorMessage = "Eriala on kohustuslik."), StringLength(60, ErrorMessage = "Eriala võib olla kuni 60 tähemärki.")]
    public string Specialty { get; set; } = "";

    /// <example>10 aastat kogemust</example>
    [StringLength(1000)]
    public string? Description { get; set; }

    /// <example>+372 5555 1234</example>
    [StringLength(30), RegularExpression(@"^\+?[0-9 \-]{5,30}$", ErrorMessage = "Vigane telefoninumber, nt +372 5555 1234")]
    public string? Phone { get; set; }

    /// <summary>Treeneri konto e-post (sellega logib ta sisse).</summary>
    /// <example>mari@sportclub.ee</example>
    [Required(ErrorMessage = "E-post on kohustuslik."), EmailAddress(ErrorMessage = "Vigane e-posti aadress.")]
    public string Email { get; set; } = "";
}

/// <summary>Treeneri muutmine. newPassword on valikuline: tühi = parool jääb samaks.</summary>
public class TrainerUpdateDto : TrainerSaveDto
{
    /// <summary>Uus parool (vähemalt 8 tähemärki). Tühi või null = parool jääb samaks.</summary>
    [StringLength(100)]
    public string? NewPassword { get; set; }
}

/// <summary>Uus treener koos sisselogimise kontoga.</summary>
public class TrainerCreateDto : TrainerSaveDto
{
    /// <example>Treener123</example>
    [Required(ErrorMessage = "Parool on kohustuslik."), StringLength(100, MinimumLength = 8, ErrorMessage = "Parool peab olema vähemalt 8 tähemärki.")]
    public string Password { get; set; } = "";
}

// ---------- Treeningutüübid ----------

public record TrainingTypeDto(int Id, string Name, string? Description);

/// <summary>Tüübi lisamine ja muutmine. Sama kuju annab GET /api/types/{id}.</summary>
public class TrainingTypeSaveDto
{
    /// <example>Jooga</example>
    [Required(ErrorMessage = "Nimi on kohustuslik."), StringLength(60, MinimumLength = 2, ErrorMessage = "Nimi peab olema 2–60 tähemärki.")]
    public string Name { get; set; } = "";

    /// <example>Rahulik venitus ja hingamine.</example>
    [StringLength(1000)]
    public string? Description { get; set; }
}

// ---------- Kuukaardid ----------

public record PassPlanDto(int Id, string Name, int Visits, int Days, decimal Price);

/// <summary>Tariifi lisamine ja muutmine. Sama kuju annab GET /api/pass-plans/{id}.</summary>
public class PassPlanSaveDto
{
    /// <example>8 külastust</example>
    [Required(ErrorMessage = "Nimi on kohustuslik."), StringLength(60, MinimumLength = 2, ErrorMessage = "Nimi peab olema 2–60 tähemärki.")]
    public string Name { get; set; } = "";

    /// <example>8</example>
    [Range(1, 100, ErrorMessage = "Külastusi peab olema 1–100.")]
    public int Visits { get; set; } = 8;

    /// <summary>Mitu päeva kaart kehtib.</summary>
    /// <example>30</example>
    [Range(1, 365, ErrorMessage = "Kehtivus peab olema 1–365 päeva.")]
    public int Days { get; set; } = 30;

    /// <example>49.90</example>
    [Range(typeof(decimal), "0", "1000", ErrorMessage = "Hind peab olema 0–1000 €.")]
    public decimal Price { get; set; }
}

/// <summary>
/// Kuukaardi ost koos kaardimaksega (simuleeritud). Kaardi andmeid ei salvestata –
/// alles jäävad ainult kaardi tüüp ja 4 viimast numbrit.
/// Testkaardid: 4242 4242 4242 4242 (õnnestub), 4000 0000 0000 0002 (lükatakse tagasi).
/// </summary>
public class BuyPassRequest
{
    /// <summary>Tariifi id (vt GET /api/pass-plans).</summary>
    /// <example>2</example>
    [Range(1, int.MaxValue, ErrorMessage = "planId on kohustuslik.")]
    public int PlanId { get; set; }

    /// <summary>Kaardi number (tühikud on lubatud).</summary>
    /// <example>4242 4242 4242 4242</example>
    [Required(ErrorMessage = "Kaardi number on kohustuslik.")]
    [RegularExpression(@"^[0-9 ]{12,23}$", ErrorMessage = "Kaardi number peab koosnema numbritest.")]
    public string CardNumber { get; set; } = "";

    /// <summary>Kaardiomaniku nimi.</summary>
    /// <example>Anna Kask</example>
    [Required(ErrorMessage = "Kaardiomaniku nimi on kohustuslik."), StringLength(100, MinimumLength = 2)]
    public string CardHolder { get; set; } = "";

    /// <summary>Kehtivus kujul KK/AA.</summary>
    /// <example>12/28</example>
    [Required(ErrorMessage = "Kehtivus on kohustuslik.")]
    [RegularExpression(@"^(0[1-9]|1[0-2])/[0-9]{2}$", ErrorMessage = "Kehtivus peab olema kujul KK/AA, nt 12/28.")]
    public string Expiry { get; set; } = "";

    /// <summary>Turvakood (3–4 numbrit).</summary>
    /// <example>123</example>
    [Required(ErrorMessage = "CVC on kohustuslik.")]
    [RegularExpression(@"^[0-9]{3,4}$", ErrorMessage = "CVC peab olema 3–4 numbrit.")]
    public string Cvc { get; set; } = "";
}

/// <summary>Kliendi enda kuukaart. card = millega maksti, nt "Visa •••• 4242".</summary>
public record MyPassDto(int Id, string Plan, int VisitsLeft, int VisitsTotal, string ValidFrom, string ValidUntil, string? Card);

/// <summary>Kuukaart admini nimekirjas.</summary>
public record PassDto(int Id, string Client, string Email, string Plan, int VisitsLeft, int VisitsTotal, string ValidFrom, string ValidUntil,
    decimal PricePaid, bool Active, string? Card);

// ---------- Broneeringud ----------

/// <summary>
/// Kliendi broneering. status: Booked = koht olemas, Waitlisted = ootejärjekorras (position = koht järjekorras).
/// attended: null = märkimata, true = kohal, false = puudus.
/// </summary>
public record BookingDto(int Id, int SessionId, string Type, string Trainer, string StartsAt, string Status, int? Position, bool? Attended);

public record WaitlistDto(int Id, int SessionId, int Position);

/// <summary>Broneering admini nimekirjas.</summary>
public record AdminBookingDto(int Id, int SessionId, string Client, string Type, string Trainer, string StartsAt, string Status,
    int? Position, bool? Attended, string CreatedAt);

/// <summary>Sorteerimine: uusimad või vanimad broneeringud.</summary>
public enum BookingSort
{
    Uusimad,
    Vanimad
}

// ---------- Treener ----------

/// <summary>
/// Treeneri treening. status: "Tulemas" (pole alanud), "Kohalolu märkimata", "Kohalolu märgitud".
/// unmarked = mitmel osalejal on kohalolu märkimata (attended = null).
/// </summary>
public record TrainerSessionDto(int Id, string Type, string StartsAt, int DurationMinutes, int Capacity, int Booked, int Waitlisted,
    int Unmarked, string Status);

/// <summary>Üks rida osalejate nimekirjas.</summary>
public class AttendanceItemDto
{
    /// <example>31</example>
    public int BookingId { get; set; }

    /// <summary>Ainult info jaoks, salvestamisel ei kasutata.</summary>
    /// <example>Anna Kask</example>
    public string? Name { get; set; }

    /// <summary>true = kohal, false = ei olnud.</summary>
    /// <example>true</example>
    [Required(ErrorMessage = "attended peab olema true või false.")]
    public bool? Attended { get; set; }
}

public class AttendanceRequest
{
    /// <summary>true = kohal, false = ei olnud.</summary>
    /// <example>true</example>
    [Required(ErrorMessage = "attended peab olema true või false.")]
    public bool? Attended { get; set; }
}

// ---------- Statistika ----------

/// <summary>fillRate = mitu protsenti kohtadest on keskmiselt täidetud.</summary>
public record PopularDto(string Type, int Sessions, int Bookings, int Attended, int FillRate);

// ---------- Taustateenus ----------

public record PromotedDto(int BookingId, int SessionId, string Name);

/// <summary>promoted = järjekorrast treeningule viidud, removed = järjekorrast eemaldatud (kuukaart puudus või treening algab tunni aja jooksul).</summary>
public record PromoteResultDto(List<PromotedDto> Promoted, int Removed);

public record ReminderParticipantDto(int BookingId, string Name, string Email);

public record StartingSessionDto(int SessionId, string Type, string StartsAt, List<ReminderParticipantDto> Participants);

public class NotificationRequest
{
    /// <summary>Broneeringu id (vt GET /api/sessions/starting).</summary>
    /// <example>31</example>
    [Range(1, int.MaxValue, ErrorMessage = "bookingId on kohustuslik.")]
    public int BookingId { get; set; }
}

public record NotificationDto(int Id, int BookingId, string Name, string Message, string CreatedAt);

// ---------- Reaalajas ----------

/// <summary>WebSocketi sõnum kanalis /ws/sessions.</summary>
public record FreeSpotsChangedMessage(int SessionId, int Free)
{
    public string Type => "FreeSpotsChanged";
}

/// <summary>Kuupäevade ja kellaaegade vorming.</summary>
public static class ClubTime
{
    public const string DateFormat = "yyyy-MM-dd";
    public const string DateTimeFormat = "yyyy-MM-dd HH:mm";

    public static string Format(DateTime value) => value.ToString(DateTimeFormat, System.Globalization.CultureInfo.InvariantCulture);
    public static string FormatDate(DateTime value) => value.ToString(DateFormat, System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>Loeb kuupäeva "2026-11-27". Vale kuju korral tuleb 400.</summary>
    public static DateTime? ParseDate(string? text, string name)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        if (DateTime.TryParseExact(text.Trim(), DateFormat, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var date))
            return date;
        throw new Exceptions.BusinessRuleException($"Parameeter '{name}': kuupäev peab olema kujul AAAA-KK-PP, nt 2026-11-27");
    }
}
