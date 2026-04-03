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
    //public Status Status { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {

    }
}

