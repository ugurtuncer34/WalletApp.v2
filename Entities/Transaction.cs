namespace WalletApp.Entities;

public class Transaction : BaseEntity
{
    public DateTime TransactionDate { get; set; }
    public decimal Amount { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }
    
    // Foreign Keys
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public Guid CurrencyId { get; set; }
    public Currency Currency { get; set; } = null!;
    public Guid? MerchantId { get; set; }
    public Merchant? Merchant { get; set; }
    public Guid CountryId { get; set; }
    public Country Country { get; set; } = null!;

    // Many-to-many tags
    public ICollection<TransactionTag> TransactionTags { get; set; } = new List<TransactionTag>();
}