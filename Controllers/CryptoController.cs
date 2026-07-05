using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WalletApp.Dtos;
using WalletApp.Services;

namespace WalletApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class CryptoController : ControllerBase
{
    private readonly ICryptoService _cryptoService;

    public CryptoController(ICryptoService cryptoService)
    {
        _cryptoService = cryptoService;
    }

    [HttpGet]
    public async Task<ActionResult<CryptoPortfolioResponse>> GetPortfolio()
    {
        var portfolio = await _cryptoService.GetMyCryptoPortfolioAsync();
        return Ok(portfolio);
    }

    [HttpPost]
    public async Task<IActionResult> AddOrUpdateCrypto(AddOrUpdateCryptoRequest request)
    {
        await _cryptoService.AddOrUpdateCryptoAsync(request);
        return Ok(new { Message = "Crypto holdings updated successfully." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCrypto(Guid id)
    {
        await _cryptoService.DeleteCryptoAsync(id);
        return NoContent();
    }
}