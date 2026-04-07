using BarberShop.Data;
using BarberShop.Models;
using Microsoft.AspNetCore.Mvc;

namespace BarberShop.Controllers;

[ApiController]
[Route("api/test")]
public class TestController : ControllerBase
{
    private readonly AppDbContext _context;

    public TestController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> CreateObjs()
    {
        var service1 = new Service
        {
            Name = "Corte e Barba",
            Duration = 45,
            Price = 50,
            Description = "Corte de cabelo na máquina e barba inclui navalha"
        };
        var service2 = new Service
        {
            Name = "Hidratação",
            Duration = 30,
            Price = 25,
            Description = "Inclui a seleção de um produto para hidratação"
        };

        var worker1 = new Worker
        {
            Name = "Cabelereiro CorteBarba",
            DateOfBirth = new DateTime(1990, 1, 1),
            Address = "123 Test St",
            Position = "Barber",
            PhoneNumber = "1234567890",
            WagePerHour = 25,
            Email = "workeremail2@gmail.com",
            ProvidedServices = new List<Service> { service1 }
        };
        var worker2 = new Worker
        {
            Name = "Cabelereiro Todos",
            DateOfBirth = new DateTime(1994, 1, 1),
            Address = "3333 Teste St",
            Position = "Barber Premium",
            PhoneNumber = "459983269292",
            WagePerHour = 30,
            Email = "workeremail@gmail.com",
            ProvidedServices = new List<Service> { service1, service2 }
        };

        var customer1 = new Customer
        {
            Name = "Cliente Salvo",
            DateOfBirth = new DateTime(1995, 1, 1),
            Email = "emailclientesalvo@gmail.com"
        };
        var customer2 = new Customer
        {
            Name = "Cliente CorteBarba",
            DateOfBirth = new DateTime(1992, 1, 1),
            Email = "cortebarba@gmail.com"
        };
        var customer3 = new Customer
        {
            Name = "Cliente CorteBarbaHidratacao",
            DateOfBirth = new DateTime(1998, 1, 1),
            Email = "cortebarbahidratacao@gmail.com"
        };
        await _context.Services.AddRangeAsync(service1, service2);
        await _context.Workers.AddRangeAsync(worker1, worker2);
        await _context.Customers.AddRangeAsync(customer1, customer2, customer3);
        await _context.SaveChangesAsync();

        return Ok("Criados");
    }
}
