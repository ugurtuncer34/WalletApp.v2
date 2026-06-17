using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletApp.Entities;
using WalletApp.Services;

namespace WalletApp.Controllers;

[Authorize]
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
        return CreatedAtAction(nameof(GetCountry), new { id = createdCountry.Id }, createdCountry);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Country>> PutCountry(Guid id, Country country)
    {
        if (id != country.Id)
            return BadRequest("Url ID does not match with Body ID");

        var updatedCountry = await _masterDataService.UpdateCountryAsync(id, country);
        return Ok(updatedCountry);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCountry(Guid id)
    {
        await _masterDataService.DeleteCountryAsync(id);
        return NoContent();
    }
}