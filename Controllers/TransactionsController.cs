using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Entities;
using WalletApp.Dtos;

namespace WalletApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly AppDbContext _context;
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
                CategoryIcon = t.Category != null ? t.Category.Icon : "🌀"
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
                CategoryIcon = t.Category != null ? t.Category.Icon : "🌀"
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
}