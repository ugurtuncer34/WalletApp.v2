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
    public async Task<ActionResult<IEnumerable<Transaction>>> GetTransactions()
    {
        return await _context.Transactions
            .Include(t => t.Category) // Include like Sql Join. If not added, area returns null
            .Include(t => t.Currency)
            .Include(t => t.Country)
            .Include(t => t.Merchant)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();
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