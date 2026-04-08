using AutoMapper;
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
    private readonly IMapper _mapper;

    public CustomersController(AppDbContext context, IWebHostEnvironment environment, RedisService redis, IHubContext<CustomersHub> hubContext, IConfiguration configuration, IMapper mapper)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
        _mapper = mapper;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var customerList = await _context.Customers.ToListAsync();
        var dtoList = _mapper.Map<List<CustomerDTO>>(customerList);
        return Ok(await _context.Customers.ToListAsync());
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();
        var dto = _mapper.Map<CustomerDTO>(customer);
        return Ok(dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
            return NotFound();
        _context.Customers.Remove(customer);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("CustomersChanged");
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
        await _hubContext.Clients.All.SendAsync("CustomersChanged");
        return Ok(updatedCustomer);
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
        await _hubContext.Clients.All.SendAsync("CustomersChanged");
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, newCustomer);
    }


}
