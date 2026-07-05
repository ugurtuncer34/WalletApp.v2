using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Dtos;
using WalletApp.Entities;

namespace WalletApp.Services;

public interface ICryptoService
{
    Task<CryptoPortfolioResponse> GetMyCryptoPortfolioAsync();
    Task AddOrUpdateCryptoAsync(AddOrUpdateCryptoRequest request);
    Task DeleteCryptoAsync(Guid id);
}

public class CryptoService : ICryptoService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IExchangeRateService _exchangeRateService;

    public CryptoService(
        AppDbContext context,
        ICurrentUserService currentUserService,
        IExchangeRateService exchangeRateService)
    {
        _context = context;
        _currentUserService = currentUserService;
        _exchangeRateService = exchangeRateService;
    }

    public async Task<CryptoPortfolioResponse> GetMyCryptoPortfolioAsync()
    {
        // bring user's coins
        var holdings = await _context.CryptoHoldings
            .Where(c => c.UserId == _currentUserService.UserId)
            .ToListAsync();

        // parallel request to Go for every coin
        var tasks = holdings.Select(async holding =>
        {
            decimal currentPrice = 0;
            try
            {
                // via gRPC
                currentPrice = await _exchangeRateService.GetCryptoRateAsync(holding.CoinCode);
            }
            catch
            {
                // if API doesn't respond or can't find the coin return 0 without crashing other coins
            }

            return new CryptoHoldingResponse
            {
                Id = holding.Id,
                CoinCode = holding.CoinCode,
                Amount = holding.Amount,
                CurrentPrice = currentPrice,
                TotalValue = holding.Amount * currentPrice
            };
        });

        // wait until all parallel tasks to finish
        var results = await Task.WhenAll(tasks);

        var response = new CryptoPortfolioResponse
        {
            // the most valued is on top
            Holdings = results.OrderByDescending(r => r.TotalValue).ToList(),
            TotalPortfolioValueUsd = results.Sum(r => r.TotalValue)
        };

        return response;
    }

    public async Task AddOrUpdateCryptoAsync(AddOrUpdateCryptoRequest request)
    {
        var coinCode = request.CoinCode.ToUpperInvariant();

        var existingHolding = await _context.CryptoHoldings
            .FirstOrDefaultAsync(c => c.UserId == _currentUserService.UserId && c.CoinCode == coinCode);

        if (existingHolding != null)
        {
            existingHolding.Amount = request.Amount;
            existingHolding.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            var newHolding = new CryptoHolding
            {
                UserId = _currentUserService.UserId,
                CoinCode = coinCode,
                Amount = request.Amount
            };
            _context.CryptoHoldings.Add(newHolding);
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteCryptoAsync(Guid id)
    {
        var holding = await _context.CryptoHoldings
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == _currentUserService.UserId);

        if (holding == null)
            throw new KeyNotFoundException("Crypto holding not found or does not belong to you.");

        _context.CryptoHoldings.Remove(holding);
        await _context.SaveChangesAsync();
    }
}