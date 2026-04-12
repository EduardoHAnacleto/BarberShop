using AutoMapper;
using BarberShop.Data;
using BarberShop.DTOs;
using BarberShop.Hubs;
using BarberShop.Models;
using BarberShop.Repositories.Interfaces;
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
    private readonly ICustomerRepository _repository;

    public CustomersController(AppDbContext context, IWebHostEnvironment environment, RedisService redis,
        IHubContext<CustomersHub> hubContext, IConfiguration configuration, IMapper mapper, ICustomerRepository repository)
    {
        _context = context;
        _environment = environment;
        _redis = redis;
        _hubContext = hubContext;
        _configuration = configuration;
        _mapper = mapper;
        _repository = repository;
    }

    [HttpGet("all")]
    public async Task<IActionResult> GetAll()
    {
        var customerList = await _repository.GetAllAsync();
        var dtoList = _mapper.Map<List<CustomerDTO>>(customerList);
        return Ok(dtoList);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var customer = await _repository.GetByIdAsync(id);
        if (customer == null)
            return NotFound();
        var dto = _mapper.Map<CustomerDTO>(customer);
        return Ok(dto);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var customer = await _repository.GetByIdAsync(id);
        if (customer == null)
            return NotFound();
        _repository.Delete(customer);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("CustomersChanged");
        return NoContent();
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CustomerDTO updatedCustomer)
    {
        var customer = await _repository.GetByIdAsync(id);
        if (customer == null)
            return NotFound();
        
        _mapper.Map(updatedCustomer, customer);
        customer.LastUpdatedAt = DateTime.UtcNow;
        _repository.Update(customer);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("CustomersChanged");
        return Ok(_mapper.Map<CustomerDTO>(customer));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerDTO newCustomer)
    {
        var customer = new Customer();
        customer = _mapper.Map(newCustomer, customer);

        await _repository.AddAsync(customer);
        await _context.SaveChangesAsync();
        await _hubContext.Clients.All.SendAsync("CustomersChanged");
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, _mapper.Map<CustomerDTO>(customer));
    }


}
