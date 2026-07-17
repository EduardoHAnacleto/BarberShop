using BarberShop.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace BarberShop.Infrastructure.Data;

public class AppDbContext : DbContext
{
    // SQL Server's datetime2 columns carry no timezone/Kind information, so
    // every DateTime EF Core reads back comes out as DateTimeKind.Unspecified
    // — even though the app always writes DateTime.UtcNow for "instant"
    // columns (CreatedAt, CompletedAt, etc.). System.Text.Json only appends
    // the "Z" suffix for Kind=Utc, so an Unspecified value round-trips to the
    // API response as an ambiguous string, which every frontend dayjs(...)
    // call then silently misreads as browser-local time instead of UTC.
    // These converters restore Kind=Utc on read for genuine instant columns.
    // Deliberately NOT applied to wall-clock/shop-local columns (Appointment.
    // ScheduledFor, Waitlist.PreferredDate, WorkingHours.ClosedFrom/Until,
    // DateOfBirth) — those intentionally stay Unspecified/naive so they
    // display the same shop-local value to every viewer regardless of their
    // own timezone, matching IShopClock's documented design.
    private static readonly ValueConverter<DateTime, DateTime> UtcDateTime = new(
        v => v,
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

    private static readonly ValueConverter<DateTime?, DateTime?> NullableUtcDateTime = new(
        v => v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v);

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Worker> Workers { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<BusinessSchedule> BusinessSchedules { get; set; }
    public DbSet<Review> Reviews { get; set; }
    public DbSet<Waitlist> Waitlist { get; set; }
    public DbSet<WorkerSchedule> WorkerSchedules { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BusinessSchedule>(entity =>
        {
            entity.ToTable("BusinessSchedules");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.DayOfWeek).HasColumnName("DayOfWeek");
            entity.Property(e => e.IsOpen).HasColumnName("IsOpen");
            entity.Property(e => e.OpenTime).HasColumnName("OpenTime");
            entity.Property(e => e.CloseTime).HasColumnName("CloseTime");
            entity.Property(e => e.BreakStart).HasColumnName("BreakStart");
            entity.Property(e => e.BreakEnd).HasColumnName("BreakEnd");

            entity.HasIndex(e => e.DayOfWeek).IsUnique();
        });

        modelBuilder.Entity<Service>(entity =>
        {
            entity.ToTable("Services");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ServiceId");
            entity.Property(e => e.Name).HasColumnName("ServiceName").HasMaxLength(100);
            entity.Property(e => e.Price).HasColumnName("ServicePrice").HasPrecision(10, 2);
            entity.Property(e => e.Description).HasColumnName("ServiceDescription").HasMaxLength(500);
            entity.Property(e => e.Duration).HasColumnName("ServiceDuration");
            // Defensive DB-level default — the C# model always sets CreatedAt
            // explicitly on insert, so this only matters for direct SQL.
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()").HasConversion(UtcDateTime);
            entity.Property(e => e.LastUpdatedAt).HasConversion(NullableUtcDateTime);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("CustomerId");
            entity.Property(e => e.Name).HasColumnName("CustomerName").HasMaxLength(100);
            entity.Property(e => e.DateOfBirth).HasColumnName("CustomerDateOfBirth");
            entity.Property(e => e.Email).HasColumnName("CustomerEmail").HasMaxLength(200);
            entity.Property(e => e.PhoneNumber).HasColumnName("CustomerPhoneNumber").HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()").HasConversion(UtcDateTime);
            entity.Property(e => e.LastUpdatedAt).HasConversion(NullableUtcDateTime);
        });

        modelBuilder.Entity<Worker>(entity =>
        {
            entity.ToTable("Workers");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("WorkerId");
            entity.Property(e => e.Name).HasColumnName("WorkerName").HasMaxLength(100);
            entity.Property(e => e.DateOfBirth).HasColumnName("WorkerDateOfBirth");
            entity.Property(e => e.Address).HasColumnName("WorkerAddress").HasMaxLength(200);
            entity.Property(e => e.Position).HasColumnName("WorkerPosition").HasMaxLength(100);
            entity.Property(e => e.PhoneNumber).HasColumnName("WorkerPhoneNumber").HasMaxLength(20);
            entity.Property(e => e.WagePerHour).HasColumnName("WorkerWagePerHour").HasPrecision(10, 2);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()").HasConversion(UtcDateTime);
            entity.Property(e => e.LastUpdatedAt).HasConversion(NullableUtcDateTime);

            entity.HasMany(e => e.ProvidedServices)
                  .WithMany()
                  .UsingEntity(e => e.ToTable("WorkersService"));
        });

        modelBuilder.Entity<WorkerService>(entity =>
        {
            entity.ToTable("WorkerServices");

            entity.HasKey(e => new { e.WorkerId, e.ServiceId });

            entity.Property(e => e.WorkerId).HasColumnName("WSWorkerId");
            entity.Property(e => e.ServiceId).HasColumnName("WSServiceId");

            entity.HasOne(e => e.Worker)
                  .WithMany()
                  .HasForeignKey(e => e.WorkerId)
                  .HasConstraintName("FK_WorkerServices_Workers");

            entity.HasOne(e => e.Service)
                  .WithMany()
                  .HasForeignKey(e => e.ServiceId)
                  .HasConstraintName("FK_WorkerServices_Services");
        });

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.ToTable("Appointments");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("AppointmentId");
            entity.Property(e => e.ServiceId).HasColumnName("AppointmentServiceId");
            entity.Property(e => e.CustomerId).HasColumnName("AppointmentCustomerId");
            entity.Property(e => e.WorkerId).HasColumnName("AppointmentWorkerId");

            entity.Property(e => e.Status).HasColumnName("AppointmentStatus");
            entity.Property(e => e.ScheduledFor).HasColumnName("AppointmentScheduledFor");
            entity.Property(e => e.ExtraDetails).HasColumnName("AppointmentExtraDetails").HasMaxLength(500);
            // ScheduledFor is deliberately left out of the UTC conversion below
            // — it's shop-local wall-clock time, not a UTC instant.
            entity.Property(e => e.CompletedAt).HasColumnName("AppointmentCompletedAt").HasConversion(NullableUtcDateTime);
            entity.Property(e => e.RecurrenceId).HasColumnName("AppointmentRecurrenceId");
            entity.Property(e => e.Reminder24hSentAt).HasColumnName("AppointmentReminder24hSentAt").HasConversion(NullableUtcDateTime);
            entity.Property(e => e.Reminder1hSentAt).HasColumnName("AppointmentReminder1hSentAt").HasConversion(NullableUtcDateTime);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()").HasConversion(UtcDateTime);
            entity.Property(e => e.LastUpdatedAt).HasConversion(NullableUtcDateTime);

            // Perf indexes matching database/schema.sql — ScheduledFor/Status
            // aren't FK columns so EF doesn't auto-index them like it does
            // WorkerId/CustomerId/ServiceId.
            entity.HasIndex(e => e.ScheduledFor);
            entity.HasIndex(e => e.Status);

            entity.HasOne(e => e.Customer)
                  .WithMany()
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Worker)
                  .WithMany()
                  .HasForeignKey(e => e.WorkerId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Service)
                  .WithMany()
                  .HasForeignKey(e => e.ServiceId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("UserId");
            entity.Property(e => e.UserRole).HasColumnName("UserRole");
            entity.Property(e => e.CustomerId).HasColumnName("UserCustomerId");
            entity.Property(e => e.WorkerId).HasColumnName("UserWorkerId");
            entity.Property(e => e.CreatedAt).HasColumnName("UserCreatedAt");
            entity.Property(e => e.Email).HasColumnName("UserEmail").HasMaxLength(200);
            entity.Property(e => e.FailedLoginAttempts).HasColumnName("UserFailedLoginAttempts");
            entity.Property(e => e.GoogleId).HasColumnName("UserGoogleId").HasMaxLength(200);
            entity.Property(e => e.PasswordHash).HasColumnName("UserPasswordHash").HasMaxLength(500);
            entity.Property(e => e.IsActive).HasColumnName("UserIsActive");
            entity.Property(e => e.LockoutEnd).HasColumnName("UserLockoutEnd").HasConversion(NullableUtcDateTime);
            entity.Property(e => e.SecurityStamp).HasColumnName("UserSecurityStamp").HasMaxLength(64)
                  .HasDefaultValueSql("CONVERT(NVARCHAR(64), NEWID())");
            entity.Property(e => e.PasswordResetToken).HasColumnName("UserPasswordResetToken").HasMaxLength(128);
            entity.Property(e => e.PasswordResetTokenExpiresAt).HasColumnName("UserPasswordResetTokenExpiresAt").HasConversion(NullableUtcDateTime);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()").HasConversion(UtcDateTime);

            entity.HasIndex(e => e.Email);
        });

        modelBuilder.Entity<WorkingHours>(entity =>
        {
            entity.ToTable("WorkingHours");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.ClosedFrom).HasColumnName("HolidayClosedFrom");
            entity.Property(e => e.ClosedUntil).HasColumnName("HolidayClosedUntil");
            entity.Property(e => e.Reason).HasColumnName("HolidayReason").HasMaxLength(200);
        });

        modelBuilder.Entity<Review>(entity =>
        {
            entity.ToTable("Reviews");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ReviewId");
            entity.Property(e => e.AppointmentId).HasColumnName("ReviewAppointmentId");
            entity.Property(e => e.CustomerId).HasColumnName("ReviewCustomerId");
            entity.Property(e => e.WorkerId).HasColumnName("ReviewWorkerId");
            entity.Property(e => e.Rating).HasColumnName("ReviewRating");
            entity.Property(e => e.Comment).HasColumnName("ReviewComment").HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()").HasConversion(UtcDateTime);

            // One review per appointment — enforced at the DB level, not just
            // in the service layer, so a race between two requests cannot
            // slip a duplicate through.
            entity.HasIndex(e => e.AppointmentId).IsUnique();

            entity.HasOne(e => e.Appointment)
                  .WithMany()
                  .HasForeignKey(e => e.AppointmentId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Customer)
                  .WithMany()
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Worker)
                  .WithMany()
                  .HasForeignKey(e => e.WorkerId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<Waitlist>(entity =>
        {
            entity.ToTable("Waitlist");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("WaitlistId");
            entity.Property(e => e.CustomerId).HasColumnName("WaitlistCustomerId");
            entity.Property(e => e.WorkerId).HasColumnName("WaitlistWorkerId");
            entity.Property(e => e.ServiceId).HasColumnName("WaitlistServiceId");
            // PreferredDate is shop-local (any open slot that day satisfies the
            // request) — deliberately excluded from the UTC conversion.
            entity.Property(e => e.PreferredDate).HasColumnName("WaitlistPreferredDate");
            entity.Property(e => e.NotifiedAt).HasColumnName("WaitlistNotifiedAt").HasConversion(NullableUtcDateTime);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()").HasConversion(UtcDateTime);

            entity.HasIndex(e => new { e.WorkerId, e.PreferredDate });

            entity.HasOne(e => e.Customer)
                  .WithMany()
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Worker)
                  .WithMany()
                  .HasForeignKey(e => e.WorkerId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Service)
                  .WithMany()
                  .HasForeignKey(e => e.ServiceId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<WorkerSchedule>(entity =>
        {
            entity.ToTable("WorkerSchedules");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.WorkerId).HasColumnName("WorkerId");
            entity.Property(e => e.DayOfWeek).HasColumnName("DayOfWeek");
            entity.Property(e => e.IsOpen).HasColumnName("IsOpen");
            entity.Property(e => e.OpenTime).HasColumnName("OpenTime");
            entity.Property(e => e.CloseTime).HasColumnName("CloseTime");
            entity.Property(e => e.BreakStart).HasColumnName("BreakStart");
            entity.Property(e => e.BreakEnd).HasColumnName("BreakEnd");

            // One override row per worker per weekday — owned by the worker,
            // so it cascades away if the worker itself is deleted (unlike
            // Appointments/Reviews, which are historical records worth
            // preserving and block worker deletion instead).
            entity.HasIndex(e => new { e.WorkerId, e.DayOfWeek }).IsUnique();

            entity.HasOne(e => e.Worker)
                  .WithMany()
                  .HasForeignKey(e => e.WorkerId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

