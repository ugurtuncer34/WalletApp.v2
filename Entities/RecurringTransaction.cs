using WalletApp.Enums;

namespace WalletApp.Entities;

public class RecurringTransaction : BaseEntity
{
    // Basic transaction info
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Amount { get; set; }
    
    // Foreign keys
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    public Guid? MerchantId { get; set; }
    public Merchant? Merchant { get; set; }
    
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;

    // Hangfire & timing info
    public RecurringFrequency Frequency { get; set; }
    public DateTime NextExecutionDate { get; set; }
    public bool IsActive { get; set; } = true;

    // Installment management
    public bool IsInstallment { get; set; } // is installment or indefinite subscription?
    public int? TotalInstallments { get; set; }
    public int? ProcessedInstallments { get; set; } = 0; // how many paid till now
}