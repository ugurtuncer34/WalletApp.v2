using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Entities;
using WalletApp.Enums;

namespace WalletApp.Services;

public interface ISubscriptionJobService
{
    Task ProcessRecurringTransactionAsync();
}

public class SubscriptionJobService : ISubscriptionJobService
{
    private readonly AppDbContext _context;
    private readonly IDistributedCache _cache;
    public SubscriptionJobService(AppDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task ProcessRecurringTransactionAsync()
    {
        var now = DateTime.UtcNow;
        
        var dueTransactions = await _context.RecurringTransactions
            .Where(rt => rt.IsActive && rt.NextExecutionDate <= now)
            .ToListAsync();
        
        if(!dueTransactions.Any())
            return;
        
        var defaultCountry = await _context.Countries.FirstOrDefaultAsync(c => c.Code == "TR");
        var defaultCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code == "TRY");

        foreach(var rt in dueTransactions)
        {
            string autoDescription = rt.IsInstallment
                ? $"Taksit ({(rt.ProcessedInstallments ?? 0) + 1}/{rt.TotalInstallments}): {rt.Name}"
                : $"Abonelik: {rt.Name}";
                
            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                UserId = rt.UserId,
                CategoryId = rt.CategoryId,
                MerchantId = rt.MerchantId,
                Amount = rt.Amount,
                Description = string.IsNullOrWhiteSpace(rt.Description) ? autoDescription : rt.Description,
                TransactionDate = now,
                ExchangeRate = 1m,
                CountryId = defaultCountry?.Id ?? Guid.Empty,
                CurrencyId = defaultCurrency?.Id ?? Guid.Empty
            };

            _context.Transactions.Add(transaction);

            if (rt.IsInstallment)
            {
                rt.ProcessedInstallments = (rt.ProcessedInstallments ?? 0) + 1;

                if(rt.TotalInstallments.HasValue && rt.ProcessedInstallments >= rt.TotalInstallments.Value)
                {
                    rt.IsActive = false;
                }
            }

            if (rt.IsActive)
            {
                rt.NextExecutionDate = rt.Frequency switch
                {
                    RecurringFrequency.Daily => rt.NextExecutionDate.AddDays(1),
                    RecurringFrequency.Weekly => rt.NextExecutionDate.AddDays(7),
                    RecurringFrequency.Monthly => rt.NextExecutionDate.AddMonths(1),
                    RecurringFrequency.Yearly => rt.NextExecutionDate.AddYears(1),
                    _ => rt.NextExecutionDate.AddMonths(1)
                };
            }
        }

        await _context.SaveChangesAsync();

        await _cache.RemoveAsync("recurring_trx_family");
    }
}