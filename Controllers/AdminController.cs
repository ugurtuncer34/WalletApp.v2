using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;

namespace WalletApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    public AdminController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet("master-data")]
    public async Task<IActionResult> GetAllMasterData()
    {
        var masterData = new
        {
            Categories = await _context.Categories.ToListAsync(),
            Currencies = await _context.Currencies.ToListAsync(),
            Countries = await _context.Countries.ToListAsync(),
            Merchants = await _context.Merchants.ToListAsync(),
            Tags = await _context.Tags.ToListAsync(),
            Transactions = await _context.Transactions
                .OrderByDescending(t => t.TransactionDate)
                .Take(20)
                .ToListAsync()
        };

        return Ok(masterData);
    }
}