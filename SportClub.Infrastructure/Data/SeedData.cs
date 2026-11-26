using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SportClub.Core.Entities;

namespace SportClub.Infrastructure.Data;

/// <summary>
/// Algandmed. Püsivad andmed (tüübid, tariifid, treenerid) lähevad migratsiooni HasData kaudu.
/// Kasutajad, kuukaardid, tunniplaan ja broneeringud lisatakse käivitamisel (<see cref="SeedRuntimeAsync"/>),
/// sest tunniplaan sõltub tänasest kuupäevast.
/// </summary>
public static class SeedData
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<TrainingType>().HasData(
            new TrainingType { Id = 1, Name = "Jooga", Description = "Rahulik venitus, tasakaal ja hingamine." },
            new TrainingType { Id = 2, Name = "HIIT", Description = "Lühikesed intensiivsed intervallid ja pausid." },
            new TrainingType { Id = 3, Name = "Spinning", Description = "Grupitreening velotrenažööridel muusika saatel." },
            new TrainingType { Id = 4, Name = "Pilates", Description = "Kerelihaste tugevdamine ja rüht." },
            new TrainingType { Id = 5, Name = "Crossfit", Description = "Funktsionaalne jõu- ja vastupidavustreening." });

        b.Entity<PassPlan>().HasData(
            new PassPlan { Id = 1, Name = "4 külastust", Visits = 4, Days = 30, Price = 25m },
            new PassPlan { Id = 2, Name = "8 külastust", Visits = 8, Days = 30, Price = 39m },
            new PassPlan { Id = 3, Name = "12 külastust", Visits = 12, Days = 30, Price = 49m },
            new PassPlan { Id = 4, Name = "20 külastust", Visits = 20, Days = 30, Price = 69m });

        b.Entity<Trainer>().HasData(
            new Trainer { Id = 1, FullName = "Kertu Saar", Specialty = "Jooga", Description = "Joogaõpetaja, 8 aastat kogemust.", Phone = "+372 5550 0001" },
            new Trainer { Id = 2, FullName = "Martin Kask", Specialty = "HIIT ja Crossfit", Description = "Endine kergejõustiklane.", Phone = "+372 5550 0002" },
            new Trainer { Id = 3, FullName = "Liis Mets", Specialty = "Pilates", Description = "Pilatese ja rühi treener.", Phone = "+372 5550 0003" },
            new Trainer { Id = 4, FullName = "Rasmus Ilves", Specialty = "Spinning", Description = "Spinningu treener ja jalgrattur.", Phone = "+372 5550 0004" });
    }

    /// <summary>Lisab demoandmed, kui andmebaasis veel kasutajaid pole. now = Eesti aeg.</summary>
    public static async Task SeedRuntimeAsync(AppDbContext db, DateTime now)
    {
        if (await db.Users.AnyAsync()) return;

        var hasher = new PasswordHasher<User>();
        User NewUser(string name, string email, string role, string password)
        {
            var u = new User { FullName = name, Email = email, Role = role, CreatedAt = now };
            u.PasswordHash = hasher.HashPassword(u, password);
            return u;
        }

        // ----- Kasutajad -----
        var admin = NewUser("Admin Adminov", "admin@sportclub.ee", Roles.Admin, "Admin123!");
        var service = NewUser("Taustateenus", "service@sportclub.ee", Roles.Service, "Service123!");
        var trainerUsers = new Dictionary<int, User>
        {
            [1] = NewUser("Kertu Saar", "kertu@sportclub.ee", Roles.Trainer, "Treener123!"),
            [2] = NewUser("Martin Kask", "treener@sportclub.ee", Roles.Trainer, "Treener123!"),
            [3] = NewUser("Liis Mets", "liis@sportclub.ee", Roles.Trainer, "Treener123!"),
            [4] = NewUser("Rasmus Ilves", "rasmus@sportclub.ee", Roles.Trainer, "Treener123!")
        };
        var mari = NewUser("Mari Tamm", "klient@sportclub.ee", Roles.Client, "Klient123!");
        var karl = NewUser("Karl Lepp", "karl@sportclub.ee", Roles.Client, "Klient123!");
        var anna = NewUser("Anna Kivi", "anna@sportclub.ee", Roles.Client, "Klient123!");
        var peeter = NewUser("Peeter Mets", "peeter@sportclub.ee", Roles.Client, "Klient123!");
        var clients = new[] { mari, karl, anna, peeter };

        db.Users.AddRange(admin, service);
        db.Users.AddRange(trainerUsers.Values);
        db.Users.AddRange(clients);
        await db.SaveChangesAsync();

        // Iga treener saab oma sisselogimise konto.
        foreach (var trainer in await db.Trainers.ToListAsync())
            if (trainerUsers.TryGetValue(trainer.Id, out var user)) trainer.UserId = user.Id;

        // ----- Kuukaardid: ostetud 10 päeva tagasi, 20 külastust -----
        var passes = clients.ToDictionary(c => c.Id, c => new MonthlyPass
        {
            UserId = c.Id,
            PassPlanId = 4,
            PlanName = "20 külastust",
            PricePaid = 69m,
            ValidFrom = now.Date.AddDays(-10),
            ValidUntil = now.Date.AddDays(21).AddSeconds(-1),
            VisitsTotal = 20
        });
        db.MonthlyPasses.AddRange(passes.Values);

        // ----- Tunniplaan: eelmised 7 päeva ja järgmised 14 päeva (Eesti aeg) -----
        var slots = new (int Hour, int Minute, int TypeId, int TrainerId, int Duration, int Capacity)[]
        {
            (7, 0, 1, 1, 60, 15),   // Jooga
            (9, 30, 2, 2, 45, 12),  // HIIT
            (12, 0, 4, 3, 50, 10),  // Pilates
            (17, 30, 3, 4, 45, 20), // Spinning
            (19, 0, 5, 2, 60, 3)    // Crossfit – ainult 3 kohta, et ootejärjekorda oleks lihtne näidata
        };

        var sessions = new List<(Session Session, int Day, int Slot)>();
        for (var day = -7; day < 14; day++)
        {
            for (var i = 0; i < slots.Length; i++)
            {
                var s = slots[i];
                var session = new Session
                {
                    TrainingTypeId = s.TypeId,
                    TrainerId = s.TrainerId,
                    StartsAt = now.Date.AddDays(day).AddHours(s.Hour).AddMinutes(s.Minute),
                    DurationMinutes = s.Duration,
                    Capacity = s.Capacity
                };
                db.Sessions.Add(session);
                sessions.Add((session, day, i));
            }
        }
        await db.SaveChangesAsync();

        // ----- Broneeringud -----
        void Book(Session session, User client, bool? attended)
        {
            var pass = passes[client.Id];
            pass.VisitsUsed++;
            db.Bookings.Add(new Booking
            {
                SessionId = session.Id,
                UserId = client.Id,
                MonthlyPassId = pass.Id,
                Status = BookingStatus.Booked,
                CreatedAt = session.StartsAt.AddDays(-2),
                Attended = attended,
                ReminderSentAt = session.StartsAt < now ? session.StartsAt.AddHours(-1) : null
            });
        }

        // Toimunud treeningud: vanematel on kohalolu märgitud, eilsel ja tänasel veel mitte.
        foreach (var (session, day, slot) in sessions.Where(x => x.Session.StartsAt < now))
        {
            for (var c = 0; c < clients.Length; c++)
            {
                if ((day + 7 + slot + c) % 4 != 0) continue;
                bool? attended = day < -1 ? (day + slot + c) % 5 != 0 : null;
                Book(session, clients[c], attended);
            }
        }

        // Homme kell 19:00 Crossfit on täis (Karl, Anna, Peeter) – Mari saab proovida ootejärjekorda.
        var crossfit = sessions.First(x => x.Day == 1 && x.Slot == 4).Session;
        foreach (var c in new[] { karl, anna, peeter }) Book(crossfit, c, null);

        // Maril on üks tulevane broneering (ülehomme jooga).
        Book(sessions.First(x => x.Day == 2 && x.Slot == 0).Session, mari, null);

        await db.SaveChangesAsync();
    }
}
