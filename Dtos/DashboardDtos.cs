namespace WalletApp.Dtos;

public class DashboardResponse
{
    public decimal TotalMonthlyExpense { get; set; }
    public List<ChartDataDto> CategoryDistribution { get; set; } = new();
    public List<ChartDataDto> MerchantDistribution { get; set; } = new();
    public List<DailyTrendDto> DailyTrend { get; set; } = new();
    public List<TransactionResponse> RecentTransactions { get; set; } = new();
}

public class ChartDataDto
{
    public string Label { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public List<ChartDataDto> SubCategories { get; set; } = new List<ChartDataDto>();
}

public class DailyTrendDto
{
    public string Date { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}