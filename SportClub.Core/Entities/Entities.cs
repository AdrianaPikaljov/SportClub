namespace SportClub.Core.Entities;

/// <summary>Süsteemi rollid.</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Trainer = "Trainer";
    public const string Client = "Client";
    public const string Service = "Service";
}

/// <summary>Kasutaja konto (klient, treener, admin või taustateenus).</summary>
public class User
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Client;
    public DateTime CreatedAt { get; set; }

    public Trainer? Trainer { get; set; }
    public List<Booking> Bookings { get; set; } = new();
    public List<MonthlyPass> Passes { get; set; } = new();
}

/// <summary>Treener. Igal treeneril on oma sisselogimise konto (roll Trainer), mille loob admin.</summary>
public class Trainer
{
    public int Id { get; set; }
    public string FullName { get; set; } = "";
    public string Specialty { get; set; } = "";
    public string? Description { get; set; }
    public string? Phone { get; set; }

    public int? UserId { get; set; }
    public User? User { get; set; }

    public List<Session> Sessions { get; set; } = new();
}

/// <summary>Treeningu tüüp (Jooga, Spinning, ...).</summary>
public class TrainingType
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }

    public List<Session> Sessions { get; set; } = new();
}

/// <summary>Üks treening tunniplaanis.</summary>
public class Session
{
    public int Id { get; set; }
    public int TrainingTypeId { get; set; }
    public TrainingType? TrainingType { get; set; }
    public int TrainerId { get; set; }
    public Trainer? Trainer { get; set; }

    /// <summary>Algusaeg klubi kohaliku aja järgi (Eesti aeg).</summary>
    public DateTime StartsAt { get; set; }
    public int DurationMinutes { get; set; } = 60;
    /// <summary>Maksimaalne osalejate arv.</summary>
    public int Capacity { get; set; }

    public List<Booking> Bookings { get; set; } = new();
}

/// <summary>Broneeringu olek. Ootejärjekord hoitakse samas tabelis.</summary>
public enum BookingStatus
{
    /// <summary>Koht on olemas.</summary>
    Booked = 0,
    /// <summary>Ootab järjekorras.</summary>
    Waitlisted = 1
}

/// <summary>
/// Broneering või koht ootejärjekorras.
/// Tühistamisel rida kustutatakse, seega tühistatud broneeringuid andmebaasis ei ole.
/// </summary>
public class Booking
{
    public int Id { get; set; }
    public int SessionId { get; set; }
    public Session? Session { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Kuukaart, millelt külastus maha võeti (ootejärjekorras null).</summary>
    public int? MonthlyPassId { get; set; }
    public MonthlyPass? MonthlyPass { get; set; }

    public BookingStatus Status { get; set; } = BookingStatus.Booked;
    /// <summary>Millal kirje loodi. Ootejärjekorra järjestus käib selle järgi.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>null = märkimata, true = kohal, false = puudus.</summary>
    public bool? Attended { get; set; }
    /// <summary>Millal meeldetuletus saadeti (null = pole saadetud).</summary>
    public DateTime? ReminderSentAt { get; set; }
}

/// <summary>Kuukaardi tariif, mida klubi müüb (nt 8 külastust / 30 päeva).</summary>
public class PassPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int Visits { get; set; }
    public int Days { get; set; } = 30;
    public decimal Price { get; set; }
    /// <summary>false = tariif on müügist eemaldatud (ostetud kuukaardid jäävad alles).</summary>
    public bool IsActive { get; set; } = true;
}

/// <summary>Kliendi ostetud kuukaart.</summary>
public class MonthlyPass
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int? PassPlanId { get; set; }
    public PassPlan? PassPlan { get; set; }

    public string PlanName { get; set; } = "";
    public decimal PricePaid { get; set; }
    /// <summary>Makse andmed: ainult kaardi tüüp ja 4 viimast numbrit (kaarti ennast ei salvestata).</summary>
    public string? CardBrand { get; set; }
    public string? CardLast4 { get; set; }
    public string? PaymentReference { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime ValidUntil { get; set; }
    public int VisitsTotal { get; set; }
    public int VisitsUsed { get; set; }

    public int VisitsLeft => VisitsTotal - VisitsUsed;

    /// <summary>Kas kaart kehtib antud hetkel ja külastusi on alles.</summary>
    public bool IsUsableAt(DateTime time) => time >= ValidFrom && time <= ValidUntil && VisitsLeft > 0;
}

/// <summary>Saadetud teavitus (meeldetuletus või ootejärjekorrast koha saamine).</summary>
public class Notification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int? BookingId { get; set; }
    public int? SessionId { get; set; }
    public string Type { get; set; } = "Reminder";
    public string Message { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
