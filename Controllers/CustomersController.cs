using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace BarberShop.Controllers;

[ApiController]
[Route("api/customers")]
public class CustomersController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly RedisService _redis;
    private readonly IHubContext<CustomersHub> _hubContext;
    private readonly IConfiguration _configuration;

    public CustomersController(AppDbContext context, IWebHostEnvironment environment, RedisService redis, IHubContext<CustomersHub> hubContext, IConfiguration configuration)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        return Ok(await _context.Customers.ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();
        return Ok(customer);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();
        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("CustomerssChanged");
        return NoContent();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerDTO updatedCustomer)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();
        // Update fields
        customer.Name = updatedCustomer.Name;
        customer.PhoneNumber = updatedCustomer.PhoneNumber;
        customer.Email = updatedCustomer.Email;
        customer.DateOfBirth = updatedCustomer.DateOfBirth;
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("CustomerssChanged");
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
        await _context.Customers.AddAsync(customer);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("CustomerssChanged");
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }


}
