using Microsoft.EntityFrameworkCore;
using SportClub.Core.Entities;
using SportClub.Core.Interfaces;

namespace SportClub.Infrastructure.Data;

public class AppDbContext : DbContext, IUnitOfWork
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Trainer> Trainers => Set<Trainer>();
    public DbSet<TrainingType> TrainingTypes => Set<TrainingType>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<WaitlistEntry> WaitlistEntries => Set<WaitlistEntry>();
    public DbSet<MonthlyPass> MonthlyPasses => Set<MonthlyPass>();
    public DbSet<PassPlan> PassPlans => Set<PassPlan>();
    public DbSet<Notification> Notifications => Set<Notification>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Role).HasMaxLength(20).IsRequired();
        });

        b.Entity<Trainer>(e =>
        {
            e.Property(x => x.FullName).HasMaxLength(100).IsRequired();
            e.Property(x => x.Bio).HasMaxLength(1000);
            e.Property(x => x.Phone).HasMaxLength(30);
            e.HasOne(x => x.User).WithOne(u => u.Trainer).HasForeignKey<Trainer>(x => x.UserId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => x.UserId).IsUnique();
        });

        b.Entity<TrainingType>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Description).HasMaxLength(1000);
            e.Property(x => x.Intensity).HasMaxLength(20);
        });

        b.Entity<Session>(e =>
        {
            e.Property(x => x.Room).HasMaxLength(50).IsRequired();
            e.HasOne(x => x.TrainingType).WithMany(t => t.Sessions).HasForeignKey(x => x.TrainingTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Trainer).WithMany(t => t.Sessions).HasForeignKey(x => x.TrainerId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => x.StartsAt);
        });

        b.Entity<Booking>(e =>
        {
            e.HasOne(x => x.Session).WithMany(s => s.Bookings).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany(u => u.Bookings).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.MonthlyPass).WithMany().HasForeignKey(x => x.MonthlyPassId).OnDelete(DeleteBehavior.SetNull);
            e.HasIndex(x => new { x.SessionId, x.Status });
        });

        b.Entity<WaitlistEntry>(e =>
        {
            e.HasOne(x => x.Session).WithMany(s => s.WaitlistEntries).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.SessionId, x.UserId }).IsUnique();
        });

        b.Entity<PassPlan>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.Price).HasConversion<double>(); // SQLite cannot sort/compare decimal
        });

        b.Entity<MonthlyPass>(e =>
        {
            e.HasOne(x => x.User).WithMany(u => u.Passes).HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.PassPlan).WithMany().HasForeignKey(x => x.PassPlanId).OnDelete(DeleteBehavior.SetNull);
            e.Property(x => x.PricePaid).HasConversion<double?>();
            e.Ignore(x => x.VisitsLeft);
        });

        b.Entity<Notification>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(30);
            e.Property(x => x.Message).HasMaxLength(500);
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        SeedData.Configure(b);
    }
}
