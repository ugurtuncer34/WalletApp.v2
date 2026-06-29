using WalletApp.Entities;
using WalletApp.Enums;

namespace WalletApp.Dtos;

public class RecurringTransactionResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public RecurringFrequency Frequency { get; set; }
    public DateTime NextExecutionDate { get; set; }
    public bool IsInstallment { get; set; }
    public int? TotalInstallments { get; set; }
    public int? ProcessedInstallments { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? MerchantName { get; set; }
    public string AddedBy { get; set; } = string.Empty;
}