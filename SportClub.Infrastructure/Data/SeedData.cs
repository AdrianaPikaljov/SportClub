using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SportClub.Core.Entities;

namespace SportClub.Infrastructure.Data;

/// <summary>
/// Initial data. Static data (training types, trainers) goes into the migration with HasData.
/// Users, passes and a two-week timetable relative to "today" are added on startup by <see cref="SeedRuntimeAsync"/>.
/// </summary>
public static class SeedData
{
    public static void Configure(ModelBuilder b)
    {
        b.Entity<TrainingType>().HasData(
            new TrainingType { Id = 1, Name = "Jooga", Intensity = "Madal", Description = "Rahulik venitus, tasakaal ja hingamine." },
            new TrainingType { Id = 2, Name = "HIIT", Intensity = "Kõrge", Description = "Lühikesed intensiivsed intervallid ja pausid." },
            new TrainingType { Id = 3, Name = "Spinning", Intensity = "Kõrge", Description = "Grupitreening velotrenažööridel muusika saatel." },
            new TrainingType { Id = 4, Name = "Pilates", Intensity = "Keskmine", Description = "Kerelihaste tugevdamine ja rüht." },
            new TrainingType { Id = 5, Name = "Crossfit", Intensity = "Kõrge", Description = "Funktsionaalne jõu- ja vastupidavustreening." });

        b.Entity<PassPlan>().HasData(
            new PassPlan { Id = 1, Name = "Proovikaart", Visits = 4, DurationDays = 30, Price = 25m, Description = "Sobib proovimiseks: 4 treeningut kuus." },
            new PassPlan { Id = 2, Name = "Kuukaart 8", Visits = 8, DurationDays = 30, Price = 39m, Description = "Kaks treeningut nädalas." },
            new PassPlan { Id = 3, Name = "Kuukaart 12", Visits = 12, DurationDays = 30, Price = 49m, Description = "Kõige populaarsem: kolm treeningut nädalas." },
            new PassPlan { Id = 4, Name = "Kuukaart 20", Visits = 20, DurationDays = 30, Price = 69m, Description = "Neile, kes treenivad peaaegu iga päev." });

        b.Entity<Trainer>().HasData(
            new Trainer { Id = 1, FullName = "Kertu Saar", Phone = "+372 5550 0001", Bio = "Joogaõpetaja, 8 aastat kogemust." },
            new Trainer { Id = 2, FullName = "Martin Kask", Phone = "+372 5550 0002", Bio = "HIIT ja crossfit, endine kergejõustiklane." },
            new Trainer { Id = 3, FullName = "Liis Mets", Phone = "+372 5550 0003", Bio = "Pilatese ja rühi treener." },
            new Trainer { Id = 4, FullName = "Rasmus Ilves", Phone = "+372 5550 0004", Bio = "Spinningu treener ja jalgrattur." });
    }

    public static async Task SeedRuntimeAsync(AppDbContext db, DateTime nowUtc)
    {
        if (await db.Users.AnyAsync()) return;

        var hasher = new PasswordHasher<User>();
        User NewUser(string name, string email, string role, string password)
        {
            var u = new User { FullName = name, Email = email, Role = role };
            u.PasswordHash = hasher.HashPassword(u, password);
            return u;
        }

        var admin = NewUser("Admin Adminov", "admin@sportclub.ee", Roles.Admin, "Admin123!");
        var service = NewUser("Taustateenus", "worker@sportclub.ee", Roles.Service, "Worker123!");
        var trainerUser = NewUser("Martin Kask", "treener@sportclub.ee", Roles.Trainer, "Treener123!");
        var client = NewUser("Mari Tamm", "klient@sportclub.ee", Roles.Client, "Klient123!");
        var client2 = NewUser("Karl Lepp", "karl@sportclub.ee", Roles.Client, "Klient123!");
        var client3 = NewUser("Anna Kivi", "anna@sportclub.ee", Roles.Client, "Klient123!");
        db.Users.AddRange(admin, service, trainerUser, client, client2, client3);
        await db.SaveChangesAsync();

        // Link Martin Kask (trainer id 2) to the trainer login.
        var martin = await db.Trainers.FirstAsync(t => t.Id == 2);
        martin.UserId = trainerUser.Id;

        var monthStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        foreach (var c in new[] { client, client2, client3 })
        {
            db.MonthlyPasses.Add(new MonthlyPass
            {
                UserId = c.Id,
                ValidFrom = monthStart,
                ValidUntil = monthStart.AddMonths(2).AddSeconds(-1), // valid this and next month, so demo bookings work
                VisitsTotal = 12,
                PassPlanId = 3,
                PricePaid = 49m,
                PurchasedAt = monthStart
            });
        }

        // Timetable for the next 14 days. Times are stored in UTC (Estonia is UTC+2 in winter).
        var slots = new (int Hour, int Minute, int TypeId, int TrainerId, int Duration, string Room, int Max)[]
        {
            (5, 0, 1, 1, 60, "Saal 2", 15),   // 07:00 Jooga
            (7, 30, 2, 2, 45, "Saal 1", 12),  // 09:30 HIIT
            (10, 0, 4, 3, 50, "Saal 2", 10),  // 12:00 Pilates
            (15, 30, 3, 4, 45, "Rattasaal", 20), // 17:30 Spinning
            (17, 0, 5, 2, 60, "Saal 1", 3)    // 19:00 Crossfit – small, easy to fill for the waitlist demo
        };
        var today = nowUtc.Date;
        for (var d = 0; d < 14; d++)
        {
            foreach (var s in slots)
            {
                var start = DateTime.SpecifyKind(today.AddDays(d).AddHours(s.Hour).AddMinutes(s.Minute), DateTimeKind.Utc);
                if (start <= nowUtc) continue;
                db.Sessions.Add(new Session
                {
                    TrainingTypeId = s.TypeId, TrainerId = s.TrainerId, StartsAt = start,
                    DurationMinutes = s.Duration, Room = s.Room, MaxParticipants = s.Max
                });
            }
        }
        await db.SaveChangesAsync();
    }
}
