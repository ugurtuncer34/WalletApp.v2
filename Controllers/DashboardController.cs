using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Dtos;

namespace WalletApp.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly AppDbContext _context;
    public DashboardController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<DashboardResponse>> GetDashboard()
    {
        // 1- TotalExpense , sum every (amount * exchangeRate)
        var totalExpense = await _context.Transactions
            .SumAsync(t => t.Amount);

        // 2- Last 5 transactions
        var recentTransactions = await _context.Transactions
            .Include(t => t.Currency)
            .OrderByDescending(t => t.TransactionDate)
            .Take(5)
            .Select(t => new TransactionSummaryDto
            {
                Date = t.TransactionDate,
                Description = t.Description ?? t.Category.Name,
                Amount = t.Amount
            })
            .ToListAsync();

        // 3- Most expense category
        var topCategory = await _context.Transactions
            .GroupBy(t => t.Category.Name)
            .Select(g => new
            {
                CategoryName = g.Key,
                TotalAmount = g.Sum(t => t.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .FirstOrDefaultAsync();

        // 4- Fill the box and send
        var response = new DashboardResponse
        {
            TotalExpense = totalExpense,
            RecentTransactions = recentTransactions,
            TopCategoryName = topCategory?.CategoryName ?? "No Expense"
        };
        return response;
    }
}