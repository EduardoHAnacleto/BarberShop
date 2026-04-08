using BarberShop.Models;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Appointment> Appointments { get; set; }
    public DbSet<Customer> Customers { get; set; }
    public DbSet<Service> Services { get; set; }
    public DbSet<Worker> Workers { get; set; }
    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Service>(entity =>
        {
            entity.ToTable("Services");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("ServiceId");
            entity.Property(e => e.Name).HasColumnName("ServiceName");
            entity.Property(e => e.Price).HasColumnName("ServicePrice");
            entity.Property(e => e.Description).HasColumnName("ServiceDescription");
            entity.Property(e => e.Duration).HasColumnName("ServiceDuration");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Costumers");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("CostumerId");
            entity.Property(e => e.Name).HasColumnName("CostumerName");
            entity.Property(e => e.DateOfBirth).HasColumnName("CostumerDateOfBirth");
            entity.Property(e => e.Email).HasColumnName("CostumerEmail");
            entity.Property(e => e.PhoneNumber).HasColumnName("CostumerPhoneNumber");
        });

        modelBuilder.Entity<Worker>(entity =>
        {
            entity.ToTable("Workers");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("WorkerId");
            entity.Property(e => e.Name).HasColumnName("WorkerName");
            entity.Property(e => e.DateOfBirth).HasColumnName("WorkerDateOfBirth");
            entity.Property(e => e.Address).HasColumnName("WorkerAddress");
            entity.Property(e => e.Position).HasColumnName("WorkerPosition");
            entity.Property(e => e.PhoneNumber).HasColumnName("WorkerPhoneNumber");
            entity.Property(e => e.WagePerHour).HasColumnName("WorkerWagePerHour");

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
            entity.Property(e => e.ExtraDetails).HasColumnName("AppointmentExtraDetails");
            entity.Property(e => e.CompletedAt).HasColumnName("AppointmentCompletedAt");

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
            entity.Property(e => e.Role).HasColumnName("UserRole");
            entity.Property(e => e.CustomerId).HasColumnName("UserCustomerId");
            entity.Property(e => e.WorkerId).HasColumnName("UserWorkerId");
            entity.Property(e => e.CreatedAt).HasColumnName("UserCreatedAt");
            entity.Property(e => e.Email).HasColumnName("UserEmail");
            entity.Property(e => e.FailedLoginAttempts).HasColumnName("UserFailedLoginAttempts");
            entity.Property(e => e.GoogleId).HasColumnName("UserGoogleId");
            entity.Property(e => e.PasswordHash).HasColumnName("UserPasswordHash");
            entity.Property(e => e.IsActive).HasColumnName("UserIsActive");
            entity.Property(e => e.LockoutEnd).HasColumnName("UserLockoutEnd");
        });

    }
}

