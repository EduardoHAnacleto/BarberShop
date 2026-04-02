using BarberShop.Data;
using BarberShop.Hubs;
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
}
