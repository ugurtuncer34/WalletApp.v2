namespace WalletApp.Dtos;

public class CryptoHoldingResponse
{
    public Guid Id { get; set; }
    public string CoinCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal CurrentPrice { get; set; } // real-time price from Go
    public decimal TotalValue { get; set; }   // Amount * CurrentPrice
}

public class CryptoPortfolioResponse
{
    public List<CryptoHoldingResponse> Holdings { get; set; } = new();
    public decimal TotalPortfolioValueUsd { get; set; }
}

public class AddOrUpdateCryptoRequest
{
    public string CoinCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
}