using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Entities;

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

    [HttpPost("seed-master-data")]
    public async Task<IActionResult> SeedMasterData()
    {
        // RESET MASTER DATA 
        _context.Merchants.RemoveRange(_context.Merchants);
        _context.Categories.RemoveRange(_context.Categories);
        _context.Countries.RemoveRange(_context.Countries);
        _context.Currencies.RemoveRange(_context.Currencies);
        _context.Tags.RemoveRange(_context.Tags);
        await _context.SaveChangesAsync();

        // CORE SEEDING 
        var tryCurrency = new Currency { Id = Guid.NewGuid(), Code = "TRY", Symbol = "₺", Name = "Turkish Lira" };
        var eurCurrency = new Currency { Id = Guid.NewGuid(), Code = "EUR", Symbol = "€", Name = "Euro" };
        var usdCurrency = new Currency { Id = Guid.NewGuid(), Code = "USD", Symbol = "$", Name = "US Dollar" };
        await _context.Currencies.AddRangeAsync(new List<Currency> { tryCurrency, eurCurrency, usdCurrency });

        var trCountry = new Country { Id = Guid.NewGuid(), Name = "Türkiye", Code = "TR" };
        await _context.Countries.AddAsync(trCountry);

        var catOther = new Category { Id = Guid.NewGuid(), Name = "Other", Icon = "🌀" };
        await _context.Categories.AddAsync(catOther);
        await _context.SaveChangesAsync();

        // DYNAMIC & PRIVATE SEEDING 
        var filePath = "seedData.json";
        if (System.IO.File.Exists(filePath))
        {
            var jsonString = await System.IO.File.ReadAllTextAsync(filePath);
            // Parse json and AddRange
        }

        return Ok(new { Message = "Master data seeded safely!" });
    }
}