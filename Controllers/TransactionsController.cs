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
        ///// changed to use ResponseDto because there were too many info
        // return await _context.Transactions
        //     .Include(t => t.Category) // Include like Sql Join. If not added, area returns null
        //     .Include(t => t.Currency)
        //     .Include(t => t.Country)
        //     .Include(t => t.Merchant)
        //     .Include(t => t.TransactionTags).ThenInclude(tt => tt.Tag)
        //     .OrderByDescending(t => t.TransactionDate)
        //     .ToListAsync();

        var transactions = await _context.Transactions
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => new TransactionResponse
            {
                Id = t.Id,
                Date = t.TransactionDate,
                Amount = t.Amount,
                Description = t.Description,
                // Get related info from regarding tables
                CategoryName = t.Category.Name,
                CategoryIcon = t.Category.Icon,
                CurrencySymbol = t.Currency.Symbol,
                CountryName = t.Country.Name,
                // Get name and color of every Tag object from TransactionTags list
                Tags = t.TransactionTags.Select(tt => new TagResponse
                {
                    Name = tt.Tag.Name,
                    Color = tt.Tag.Color
                }).ToList()
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
                CategoryName = t.Category.Name,
                CategoryIcon = t.Category.Icon,
                CurrencySymbol = t.Currency.Symbol,
                CountryName = t.Country.Name,
                Tags = t.TransactionTags.Select(tt => new TagResponse
                {
                    Name = tt.Tag.Name,
                    Color = tt.Tag.Color ?? "#cccccc"
                }).ToList()
            })
            .FirstOrDefaultAsync();
        
        if(transaction is null)
        {
            return NotFound();
        }
        return transaction;
    }

    [HttpPost]
    public async Task<ActionResult<Transaction>> PostTransaction(CreateTransactionRequest request)
    {
        // Validating
        if(!await _context.Categories.AnyAsync(c => c.Id == request.CategoryId))
        {
            return BadRequest($"Invalid Category ID: {request.CategoryId}");
        }
        if(!await _context.Currencies.AnyAsync(c => c.Id == request.CurrencyId))
        {
            return BadRequest($"Invalid Currency ID: {request.CurrencyId}");
        }
        if(!await _context.Countries.AnyAsync(c => c.Id == request.CountryId))
        {
            return BadRequest($"Invalid Country ID: {request.CountryId}");
        }
        // Merchant is optional so check if it has value
        if(request.MerchantId.HasValue && !await _context.Merchants.AnyAsync(m => m.Id == request.MerchantId.Value))
        {
            return BadRequest($"Invalid Merchant ID: {request.MerchantId}");
        }
        if(request.TagIds != null && request.TagIds.Any())
        {
            var existingTagsCount = await _context.Tags
                .CountAsync(t => request.TagIds.Contains(t.Id));
            if(existingTagsCount != request.TagIds.Count)
            {
                return BadRequest("Some tags could not be found in the system!");
            }
        }

        // Map DTO to Entity
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = request.Date,
            Amount = request.Amount,
            ExchangeRate = request.ExchangeRate,
            Description = request.Description,
            Notes = request.Notes,
            // Relations over IDs
            CategoryId = request.CategoryId,
            CurrencyId = request.CurrencyId,
            CountryId = request.CountryId,
            MerchantId = request.MerchantId
        };

        // If any tags selected, enter loop (many-to-many)
        if(request.TagIds != null && request.TagIds.Any())
        {
            foreach(var tagId in request.TagIds)
            {
                // Add new record to TransactionTag table
                var transactionTag = new TransactionTag
                {
                    TransactionId = transaction.Id,
                    TagId = tagId
                };

                // Add to list inside Transaction
                transaction.TransactionTags.Add(transactionTag);
            }
        }

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        // need a getById for below, will do later
        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        var transaction = await _context.Transactions.FindAsync(id);
        if(transaction is null)
        {
            return NotFound();
        }
        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}