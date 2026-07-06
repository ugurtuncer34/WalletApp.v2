using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Dtos;

namespace WalletApp.Services;

public class DashboardService : IDashboardService
{
    private readonly AppDbContext _context;
    public DashboardService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<DashboardResponse> GetMonthlyDashboardAsync(int year, int month, Guid? userId = null, Guid? currencyId = null)
    {
        // Calculate the dates
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1).AddSeconds(-1);

        // Take all transactions from that month into RAM
        var query = _context.Transactions
            .Include(t => t.Category)
                .ThenInclude(c => c.ParentCategory)
            .Include(t => t.Merchant)
            .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate)
            .AsQueryable();

        if (userId.HasValue && userId.Value != Guid.Empty)
        {
            query = query.Where(t => t.UserId == userId.Value);
        }

        if (currencyId.HasValue && currencyId.Value != Guid.Empty)
        {
            query = query.Where(t => t.CurrencyId == currencyId.Value);
        }

        var monthTransactions = await query
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var response = new DashboardResponse
        {
            TotalMonthlyExpense = monthTransactions.Sum(t => t.Amount * (t.ExchangeRate ?? 1m)),

            CategoryDistribution = monthTransactions
                .GroupBy(t => t.Category.ParentCategory != null ? t.Category.ParentCategory.Name : t.Category.Name)
                .Select(parentGroup => new ChartDataDto
                {
                    Label = parentGroup.Key,
                    Value = parentGroup.Sum(t => t.Amount * (t.ExchangeRate ?? 1m)),
                    // Group sub categories in between
                    SubCategories = parentGroup
                        .GroupBy(t => t.Category.Name)
                        .Select(subGroup => new ChartDataDto
                        {
                            Label = subGroup.Key,
                            Value = subGroup.Sum(t => t.Amount * (t.ExchangeRate ?? 1m))
                        })
                        .OrderByDescending(x => x.Value)
                        .ToList()
                })
                .OrderByDescending(x => x.Value)
                .ToList(),

            MerchantDistribution = monthTransactions
                .Where(t => t.Merchant != null)
                .GroupBy(t => t.Merchant!.Name)
                .Select(g => new ChartDataDto
                {
                    Label = g.Key,
                    Value = g.Sum(t => t.Amount * (t.ExchangeRate ?? 1m))
                })
                .OrderByDescending(x => x.Value)
                .ToList(),

            DailyTrend = monthTransactions
                .GroupBy(t => t.TransactionDate.Date)
                .Select(g => new DailyTrendDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Amount = g.Sum(t => t.Amount * (t.ExchangeRate ?? 1m))
                })
                .OrderBy(x => x.Date)
                .ToList()
        };

        return response;
    }
}