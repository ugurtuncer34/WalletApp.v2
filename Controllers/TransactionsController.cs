using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WalletApp.Data;
using WalletApp.Entities;
using WalletApp.Dtos;

namespace WalletApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly CultureInfo _trCulture = new CultureInfo("tr-TR");
    public TransactionsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetTransactions()
    {
        var transactions = await _context.Transactions
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => new TransactionResponse
            {
                Id = t.Id,
                Date = t.TransactionDate,
                Amount = t.Amount,
                Description = t.Description,
                CategoryName = t.Category != null ? t.Category.Name : "OTHER",
                CategoryIcon = t.Category != null ? t.Category.Icon : "🌀",
                MerchantName = t.Merchant != null ? t.Merchant.Name : string.Empty
            })
            .ToListAsync();

        return transactions;
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionResponse>> GetTransaction(Guid id)
    {
        var transaction = await _context.Transactions
            .Where(t => t.Id == id)
            .Select(t => new TransactionResponse
            {
                Id = t.Id,
                Date = t.TransactionDate,
                Amount = t.Amount,
                Description = t.Description,
                CategoryName = t.Category != null ? t.Category.Name : "OTHER",
                CategoryIcon = t.Category != null ? t.Category.Icon : "🌀",
                MerchantName = t.Merchant != null ? t.Merchant.Name : string.Empty
            })
            .FirstOrDefaultAsync();

        if (transaction is null) return NotFound();

        return transaction;
    }

    [HttpPost]
    public async Task<ActionResult<Transaction>> PostTransaction(CreateTransactionRequest request)
    {
        // 1. Defaulting
        Guid finalCategoryId;
        if (request.CategoryId == null || request.CategoryId == Guid.Empty)
        {
            var defaultCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "DİĞER");
            if (defaultCategory == null) return BadRequest("Default category (OTHER) not found in database.");
            finalCategoryId = defaultCategory.Id;
        }
        else
        {
            finalCategoryId = request.CategoryId.Value;
        }

        // 2. Validating
        if (!await _context.Categories.AnyAsync(c => c.Id == finalCategoryId))
        {
            return BadRequest($"Invalid Category ID: {finalCategoryId}");
        }

        // 3. Mapping 
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = request.Date ?? DateTime.UtcNow,
            Amount = request.Amount,
            Description = request.Description,
            CategoryId = finalCategoryId
            // ExchangeRate, CurrencyId, CountryId, MerchantId automatically null
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        var transaction = await _context.Transactions.FindAsync(id);
        if (transaction is null)
        {
            return NotFound();
        }
        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("quick-add")]
    public async Task<ActionResult<TransactionResponse>> QuickAddTransaction([FromBody] QuickAddRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            return BadRequest("Please enter an expense word. Ex: '150 Market'");
        }

        var parts = request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return BadRequest("Please enter a valid text.");

        if (!decimal.TryParse(parts[0], out decimal amount))
        {
            return BadRequest("First word should be a valid amount. Ex: '150 Market'");
        }

        var description = parts.Length > 1 ? string.Join(" ", parts.Skip(1)).Trim() : "Not declared.";
        var descLower = description.ToLower(_trCulture);

        string targetCategoryName = "DİĞER";

        if (descLower.Contains("fırın") || descLower.Contains("file") || descLower.Contains("şok") || descLower.Contains("market"))
        {
            targetCategoryName = "ALIŞVERİŞ";
        }
        else if (descLower.Contains("coffee") || descLower.Contains("told") || descLower.Contains("kahve"))
        {
            targetCategoryName = "KAHVE";
        }

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToUpper() == targetCategoryName);
        if (category is null)
        {
            category = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToUpper() == "DİĞER");
        }
        if (category is null) return BadRequest("No category found in the system.");

        var allMerchants = await _context.Merchants.ToListAsync();

        var matchedMerchant = allMerchants.FirstOrDefault(m => descLower.Contains(m.Name.ToLower(_trCulture)));

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = DateTime.UtcNow,
            Amount = amount,
            Description = description,
            CategoryId = category.Id,
            MerchantId = matchedMerchant?.Id
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        var response = new TransactionResponse
        {
            Id = transaction.Id,
            Date = transaction.TransactionDate,
            Amount = transaction.Amount,
            Description = transaction.Description,
            CategoryName = category.Name,
            CategoryIcon = category.Icon,
            MerchantName = matchedMerchant != null ? matchedMerchant.Name : string.Empty
        };

        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, response);

    }
}