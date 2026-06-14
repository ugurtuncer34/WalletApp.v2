using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletApp.Entities;
using WalletApp.Services;

namespace WalletApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class MerchantsController : ControllerBase
{
    private readonly IMasterDataService _masterDataService;

    public MerchantsController(IMasterDataService masterDataService)
    {
        _masterDataService = masterDataService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Merchant>>> GetMerchants()
    {
        var merchants = await _masterDataService.GetMerchantsAsync();
        return Ok(merchants);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Merchant>> GetMerchant(Guid id)
    {
        var merchant = await _masterDataService.GetMerchantByIdAsync(id);
        return Ok(merchant);
    }

    [HttpPost]
    public async Task<ActionResult<Merchant>> PostMerchant(Merchant merchant)
    {
        var createdMerchant = await _masterDataService.CreateMerchantAsync(merchant);
        return CreatedAtAction(nameof(GetMerchant), new { id = createdMerchant.Id }, createdMerchant);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteMerchant(Guid id)
    {
        await _masterDataService.DeleteMerchantAsync(id);
        return NoContent();
    }
}