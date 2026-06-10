using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Entities;

namespace WalletApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class MerchantsController : ControllerBase
{
    private readonly AppDbContext _context;

    public MerchantsController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Merchant>>> GetMerchants()
    {
        return await _context.Merchants.Include(t => t.DefaultCategory).ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Merchant>> GetMerchant(Guid id)
    {
        var merchant = await _context.Merchants.FindAsync(id);
        if(merchant is null)
            return NotFound();

        return merchant;
    }

    [HttpPost]
    public async Task<ActionResult<Merchant>> PostMerchant(Merchant merchant)
    {
        _context.Merchants.Add(merchant);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetMerchant), new { id = merchant.Id }, merchant);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMerchant(Guid id)
    {
        var merchant = await _context.Merchants.FindAsync(id);
        if(merchant is null)
            return NotFound();
        
        _context.Merchants.Remove(merchant);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}