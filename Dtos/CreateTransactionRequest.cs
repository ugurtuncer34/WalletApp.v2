namespace WalletApp.Dtos;

public class CreateTransactionRequest
{
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public decimal ExchangeRate { get; set; }
    public string? Description { get; set; }
    public string? Notes { get; set; }

    public Guid CategoryId { get; set; }
    public Guid CurrencyId { get; set; }
    public Guid? MerchantId { get; set; }
    public Guid CountryId { get; set; }
}