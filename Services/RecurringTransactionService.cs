using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using WalletApp.Data;
using WalletApp.Dtos;
using WalletApp.Entities;

namespace WalletApp.Services;

public class RecurringTransactionService : IRecurringTransactionService
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDistributedCache _cache;

    private const string CacheKey = "recurring_trx_family";

    public RecurringTransactionService(AppDbContext context, ICurrentUserService currentUserService, IDistributedCache cache)
    {
        _context = context;
        _currentUserService = currentUserService;
        _cache = cache;
    }

    public async Task<List<RecurringTransactionResponse>> GetMySubscriptionsAsync()
    {
        // check cache first
        var cachedData = await _cache.GetStringAsync(CacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<List<RecurringTransactionResponse>>(cachedData)!;
        }

        // if cache is empty, go to db
        var subscriptions = await _context.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.Merchant)
            .Include(r => r.User)
            .Where(r => r.IsActive)
            .OrderBy(r => r.NextExecutionDate)
            .Select(r => new RecurringTransactionResponse
            {
                Id = r.Id,
                Name = r.Name,
                Amount = r.Amount,
                Frequency = r.Frequency,
                NextExecutionDate = r.NextExecutionDate,
                IsInstallment = r.IsInstallment,
                TotalInstallments = r.TotalInstallments,
                ProcessedInstallments = r.ProcessedInstallments,
                CategoryName = r.Category.Name,
                MerchantName = r.Merchant != null ? r.Merchant.Name : null,
                AddedBy = r.User.Username
            })
            .ToListAsync();

        // write to cache for 30 mins
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        await _cache.SetStringAsync(CacheKey, JsonSerializer.Serialize(subscriptions), cacheOptions);

        return subscriptions;
    }

    public async Task<Guid> CreateSubscriptionAsync(CreateRecurringRequest request)
    {
        var recurringTransaction = new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId,
            Name = request.Name,
            Description = request.Description,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            MerchantId = request.MerchantId,
            Frequency = request.Frequency,
            NextExecutionDate = request.StartDate.ToUniversalTime(),
            IsActive = true,
            IsInstallment = request.IsInstallment,
            TotalInstallments = request.IsInstallment ? request.TotalInstallments : null,
            ProcessedInstallments = 0
        };

        _context.RecurringTransactions.Add(recurringTransaction);
        await _context.SaveChangesAsync();

        // cache invalidation
        await _cache.RemoveAsync(CacheKey);

        return recurringTransaction.Id;
    }

    public async Task UpdateSubscriptionAsync(Guid id, UpdateRecurringRequest request)
    {
        // Only the user who added can update
        var subscription = await _context.RecurringTransactions
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == _currentUserService.UserId);

        if (subscription is null)
            throw new KeyNotFoundException("Abonelik bulunamadı veya bu aboneliği güncelleme yetkiniz yok.");

        subscription.Name = request.Name;
        subscription.Description = request.Description;
        subscription.Amount = request.Amount;
        subscription.CategoryId = request.CategoryId;
        subscription.MerchantId = request.MerchantId;
        subscription.Frequency = request.Frequency;
        subscription.NextExecutionDate = request.NextExecutionDate.ToUniversalTime();
        
        subscription.IsInstallment = request.IsInstallment;
        subscription.TotalInstallments = request.IsInstallment ? request.TotalInstallments : null;

        await _context.SaveChangesAsync();

        // cache invalidation
        await _cache.RemoveAsync(CacheKey);
    }

    public async Task CancelSubscriptionAsync(Guid id)
    {
        // only the one who added subscription can delete it
        var subscription = await _context.RecurringTransactions
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == _currentUserService.UserId);

        if (subscription is null)
            throw new KeyNotFoundException("Could not find subscription or does not belong to you.");

        subscription.IsActive = false;
        await _context.SaveChangesAsync();

        // cache invalidation
        await _cache.RemoveAsync(CacheKey);
    }
}