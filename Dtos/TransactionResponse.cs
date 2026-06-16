namespace WalletApp.Dtos;

public class TransactionResponse
{
    public Guid Id { get; set; }
    public DateTime Date { get; set; }
    public decimal Amount { get; set; }
    public string? Description { get; set; }
    public decimal? ExchangeRate { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? CategoryIcon { get; set; }
    public string? MerchantName { get; set; }
    public string CurrencySymbol { get; set; } = string.Empty;
    public string CountryName { get; set; } = string.Empty;
    // public List<TagResponse> Tags { get; set; } = new();
}

// public class TagResponse
// {
//     public string Name { get; set; } = string.Empty;
//     public string? Color { get; set; }
// }