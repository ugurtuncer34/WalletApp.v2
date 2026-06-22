using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Dtos;
using WalletApp.Entities;
using WalletApp.Services;

namespace WalletApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IMasterDataService _masterDataService;
    public AdminController(AppDbContext context, IMasterDataService masterDataService)
    {
        _context = context;
        _masterDataService = masterDataService;
    }

    [HttpGet("master-data")]
    public async Task<IActionResult> GetAllMasterData()
    {
        var masterData = new
        {
            Categories = await _masterDataService.GetCategoriesAsync(),
            Currencies = await _masterDataService.GetCurrenciesAsync(),
            Countries = await _masterDataService.GetCountriesAsync(),
            Merchants = await _masterDataService.GetMerchantsAsync(),
            Tags = await _context.Tags.ToListAsync(),
            Transactions = await _context.Transactions
                .OrderByDescending(t => t.TransactionDate)
                .Take(20)
                .ToListAsync()
        };

        return Ok(masterData);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost("seed-master-data")]
    public async Task<IActionResult> SeedMasterData(SeedMasterDataRequest request)
    {
        if (request.Passphrase != "DELETE_APPROVE")
            return BadRequest("Security breach: Missing confirmation key!");

        var isDbPopulated = await _context.Categories.AnyAsync();
        if (isDbPopulated && !request.ForceOverWrite)
            return BadRequest("DB is already populated. If you still want to overwrite, send ForceOverwrite value true.");

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

    [HttpPost("seed-category-rules")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SeedCategoryRules()
    {
        bool dbHasData = await _context.CategoryRules.AnyAsync();
        if (dbHasData)
        {
            return BadRequest("Already have category rules. Use frontend to administrate them.");
        }
        
        // Original json
        string jsonContent = @"{
            ""GROCERIES"": [
                ""market"",
                ""bakery"",
                ""amazon""
            ],
            ""FUEL"": [
                ""gas"",
                ""shell""
            ],
            ""COFFEE"": [
                ""starbucks"",
                ""coffee""
            ]
        }";

        var parsedRules = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, List<string>>>(jsonContent);
        if (parsedRules == null) return BadRequest("JSON parse edilemedi.");

        var dbCategories = await _context.Categories.ToListAsync();
        int addedCount = 0; // Kaç kural ekledik sayalım

        foreach (var rule in parsedRules)
        {
            // JSON'daki Key ile veritabanındaki kategori adını eşleştir
            var category = dbCategories.FirstOrDefault(c => c.Name.ToUpperInvariant() == rule.Key.ToUpperInvariant());

            if (category != null)
            {
                foreach (var keyword in rule.Value)
                {
                    // Eğer keyword zaten veritabanında yoksa ekle
                    if (!await _context.CategoryRules.AnyAsync(cr => cr.Keyword == keyword))
                    {
                        _context.CategoryRules.Add(new CategoryRule
                        {
                            Id = Guid.NewGuid(),
                            Keyword = keyword,
                            CategoryId = category.Id
                        });
                        addedCount++;
                    }
                }
            }
        }

        await _context.SaveChangesAsync();

        return Ok(new { Message = $"{addedCount} new category rules added successfully." });
    }
}