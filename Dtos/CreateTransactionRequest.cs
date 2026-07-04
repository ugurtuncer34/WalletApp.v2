namespace WalletApp.Dtos;

public class CreateTransactionRequest
{
    public DateTime? Date { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public Guid? CategoryId { get; set; } // not nullable but if null default val exists
    public Guid? MerchantId { get; set; } // nullable
    public Guid? CurrencyId { get; set; }
    public Guid? CountryId { get; set; }

    // // Tag Id lists
    // public List<Guid> TagIds { get; set; } = new();
}