namespace WalletApp.Entities;

public class CryptoHolding : BaseEntity
{
    public string CoinCode { get; set; } = string.Empty; // e.g. "BTC", "XRP"    
    public decimal Amount { get; set; }  // e.g. 0.005
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}