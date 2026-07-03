using WalletApp.Enums;

namespace WalletApp.Dtos;

public class UpdateRecurringRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    
    public Guid CategoryId { get; set; }
    public Guid? MerchantId { get; set; }

    public RecurringFrequency Frequency { get; set; }
    public DateTime NextExecutionDate { get; set; } 
    
    public bool IsInstallment { get; set; }
    public int? TotalInstallments { get; set; }
}