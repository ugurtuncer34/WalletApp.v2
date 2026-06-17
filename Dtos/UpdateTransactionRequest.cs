namespace WalletApp.Dtos;

public class UpdateTransactionRequest
{
    public decimal? Amount { get; set; }
    public string? Description { get; set; }
    public DateTime? Date { get; set; }
    public decimal? ExchangeRate { get; set; }
    public Guid? CategoryId { get; set; }
    public Guid? MerchantId { get; set; }
    public Guid? CountryId { get; set; }
    public Guid? CurrencyId { get; set; }
}