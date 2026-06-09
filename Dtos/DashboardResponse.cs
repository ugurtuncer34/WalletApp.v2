namespace WalletApp.Dtos;

public class DashboardResponse
{
    public decimal TotalExpense { get; set; }
    public string TopCategoryName { get; set; } = string.Empty;
    public List<TransactionSummaryDto> RecentTransactions { get; set; } = new();
}

public class TransactionSummaryDto
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
}