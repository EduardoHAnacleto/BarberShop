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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Service>(entity =>
        {
            entity.ToTable("Services");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasColumnName("ServiceName");
            entity.Property(e => e.Price).HasColumnName("ServicePrice");
            entity.Property(e => e.Description).HasColumnName("ServiceDescription");
            entity.Property(e => e.Duration).HasColumnName("ServiceDuration");
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Costumers");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasColumnName("CostumerName");
            entity.Property(e => e.DateOfBirth).HasColumnName("CostumerDateOfBirth");
            entity.Property(e => e.Email).HasColumnName("CostumerEmail");
            entity.Property(e => e.PhoneNumber).HasColumnName("CostumerPhoneNumber");
        });

        modelBuilder.Entity<Worker>(entity =>
        {
            entity.ToTable("Workers");

            entity.HasKey(e => e.Id);
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

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.ToTable("Appointments");

            entity.HasKey(e => e.Id);
            entity.Property(e => e.Service.Id).HasColumnName("AppointmentServiceId");
            entity.Property(e => e.Status).HasColumnName("AppointmentStatus");
            entity.Property(e => e.ScheduledFor).HasColumnName("AppointmentScheduledFor");
            entity.Property(e => e.ExtraDetails).HasColumnName("AppointmentExtraDetails");
            entity.Property(e => e.CompletedAt).HasColumnName("AppointmentCompletedAt");
            entity.Property(e => e.Customer.Id).HasColumnName("AppointmentCustomerId");
            entity.Property(e => e.Worker.Id).HasColumnName("AppointmentWorkerId");

            entity.HasOne(e => e.Customer)
                  .WithMany()
                  .HasForeignKey(e => e.Customer.Id)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Worker)
                  .WithMany()
                  .HasForeignKey(e => e.Worker.Id)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Service)
                  .WithMany()
                  .HasForeignKey(e => e.Service.Id)
                  .OnDelete(DeleteBehavior.NoAction);
        });

    }
}

