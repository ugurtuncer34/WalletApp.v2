using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Entities;

namespace WalletApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CurrenciesController : ControllerBase
{
    private readonly AppDbContext _context;
    public CurrenciesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Currency>>> GetCurrencies()
    {
        return await _context.Currencies.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Currency>> GetCurrency(Guid id)
    {
        var currency = await _context.Currencies.FindAsync(id);
        if(currency is null)
            return NotFound();
        return currency;
    }

    [HttpPost]
    public async Task<ActionResult<Currency>> PostCurrency(Currency currency)
    {
        currency.Code = currency.Code.ToUpper();
        var exists = await _context.Currencies.AnyAsync(c => c.Code == currency.Code);
        if (exists)
        {
            return BadRequest($"Error: '{currency.Code}' code already exists!");
        }
        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCurrency), new { id = currency.Id }, currency);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCurrency(Guid id)
    {
        var currency = await _context.Currencies.FindAsync(id);
        if(currency is null)
            return NotFound();
        
        _context.Currencies.Remove(currency);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}