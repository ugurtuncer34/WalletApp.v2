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

    [HttpPost]
    public async Task<ActionResult<Transaction>> PostTransaction(CreateTransactionRequest request)
    {
        // Better to valide IDs here, like category or country

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
        return CreatedAtAction(nameof(GetTransactions), new { id = transaction.Id }, transaction);
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