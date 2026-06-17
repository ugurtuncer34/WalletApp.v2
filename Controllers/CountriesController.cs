using Microsoft.AspNetCore.Mvc;
using WalletApp.Entities;
using WalletApp.Services;

namespace WalletApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CountriesController : ControllerBase
{
    private readonly IMasterDataService _masterDataService;
    public CountriesController(IMasterDataService masterDataService)
    {
        _masterDataService = masterDataService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Country>>> GetCountries()
    {
        var countries = await _masterDataService.GetCountriesAsync();
        return Ok(countries);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Country>> GetCountry(Guid id)
    {
        var country = await _masterDataService.GetCountryByIdAsync(id);
        return Ok(country);
    }

    [HttpPost]
    public async Task<ActionResult<Country>> PostCountry(Country country)
    {
        var createdCountry = await _masterDataService.CreateCountryAsync(country);
        return CreatedAtAction(nameof(GetCountry), new {id = createdCountry.Id}, createdCountry);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCountry(Guid id)
    {
        await _masterDataService.DeleteCountryAsync(id);
        return NoContent();
    }
}