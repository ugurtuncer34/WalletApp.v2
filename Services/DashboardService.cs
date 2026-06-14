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

    public async Task<DashboardResponse> GetMonthlyDashboardAsync(int year, int month)
    {
        // Calculate the dates
        var startDate = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var endDate = startDate.AddMonths(1).AddSeconds(-1);

        // Take all transactions from that month into RAM
        var monthTransactions = await _context.Transactions
            .Include(t => t.Category)
            .Include(t => t.Merchant)
            .Where(t => t.TransactionDate >= startDate && t.TransactionDate <= endDate)
            .OrderByDescending(t => t.TransactionDate)
            .ToListAsync();

        var response = new DashboardResponse
        {
            TotalMonthlyExpense = monthTransactions.Sum(t => t.Amount),

            RecentTransactions = monthTransactions
                .Take(5)
                .Select(t => new TransactionResponse
                {
                    Id = t.Id,
                    Date = t.TransactionDate,
                    Amount = t.Amount,
                    Description = t.Description,
                    CategoryName = t.Category.Name,
                    CategoryIcon = t.Category.Icon,
                    MerchantName = t.Merchant?.Name ?? string.Empty
                })
                .ToList(),

            CategoryDistribution = monthTransactions
                .GroupBy(t => t.Category.Name)
                .Select(g => new ChartDataDto
                {
                    Label = g.Key,
                    Value = g.Sum(t => t.Amount)
                })
                .OrderByDescending(x => x.Value)
                .ToList(),

            MerchantDistribution = monthTransactions
                .Where(t => t.Merchant != null)
                .GroupBy(t => t.Merchant!.Name)
                .Select(g => new ChartDataDto
                {
                    Label = g.Key,
                    Value = g.Sum(t => t.Amount)
                })
                .OrderByDescending(x => x.Value)
                .ToList(),

            DailyTrend = monthTransactions
                .GroupBy(t => t.TransactionDate.Date)
                .Select(g => new DailyTrendDto
                {
                    Date = g.Key.ToString("yyyy-MM-dd"),
                    Amount = g.Sum(t => t.Amount)
                })
                .OrderBy(x => x.Date)
                .ToList()
        };

        return response;
    }
}