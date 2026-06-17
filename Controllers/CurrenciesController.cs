using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Entities;
using WalletApp.Services;

namespace WalletApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CurrenciesController : ControllerBase
{
    private readonly IMasterDataService _masterDataService;
    public CurrenciesController(IMasterDataService masterDataService)
    {
        _masterDataService = masterDataService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Currency>>> GetCurrencies()
    {
        var currencies = await _masterDataService.GetCurrenciesAsync();
        return Ok(currencies);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Currency>> GetCurrency(Guid id)
    {
        var currency = await _masterDataService.GetCurrencyByIdAsync(id);
        return Ok(currency);
    }

    [HttpPost]
    public async Task<ActionResult<Currency>> PostCurrency(Currency currency)
    {
        var createdCurrency = await _masterDataService.CreateCurrencyAsync(currency);
        return CreatedAtAction(nameof(GetCurrency), new { id = createdCurrency.Id }, createdCurrency);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCurrency(Guid id)
    {
        await _masterDataService.DeleteCurrencyAsync(id);
        return NoContent();
    }
}