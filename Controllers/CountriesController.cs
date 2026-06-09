using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Entities;

namespace WalletApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CountriesController : ControllerBase
{
    private readonly AppDbContext _context;
    public CountriesController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Country>>> GetCountries()
    {
        return await _context.Countries.ToListAsync();
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Country>> GetCountry(Guid id)
    {
        var country = await _context.Countries.FindAsync(id);
        if(country is null)
            return NotFound();
        return country;
    }

    [HttpPost]
    public async Task<ActionResult<Country>> PostCountry(Country country)
    {
        _context.Countries.Add(country);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetCountry), new {id = country.Id}, country);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCountry(Guid id)
    {
        var country = await _context.Countries.FindAsync(id);
        if(country is null)
            return NotFound();
        
        _context.Countries.Remove(country);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}