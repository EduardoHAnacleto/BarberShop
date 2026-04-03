using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace BarberShop.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : Controller
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<WorkersHub> _hubContext;
    private readonly IConfiguration _configuration;

    public CustomersController(AppDbContext context, IWebHostEnvironment environment, RedisService redis, IHubContext<WorkersHub> hubContext, IConfiguration configuration)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        return Ok(_context.Customers.ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = _context.Customers.Find(id);
        if (customer == null)
            return NotFound();
        return Ok(customer);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = _context.Customers.Find(id);
        if (customer == null)
            return NotFound();
        _context.Customers.Remove(customer);
        _context.SaveChanges();
        return NoContent();
    }

    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerDTO updatedCustomer)
    {
        var customer = _context.Customers.Find(id);
        if (customer == null)
            return NotFound();
        // Update fields
        customer.Name = updatedCustomer.Name;
        customer.PhoneNumber = updatedCustomer.PhoneNumber;
        customer.Email = updatedCustomer.Email;
        customer.DateOfBirth = updatedCustomer.DateOfBirth;
        _context.SaveChanges();
        return Ok(customer);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerDTO newCustomer)
    {
        var customer = new Customer
        {
            Name = newCustomer.Name,
            PhoneNumber = newCustomer.PhoneNumber,
            Email = newCustomer.Email,
            DateOfBirth = newCustomer.DateOfBirth
        };
        _context.Customers.Add(customer);
        _context.SaveChanges();
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }


}
