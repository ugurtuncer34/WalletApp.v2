namespace WalletApp.Entities;

public class Currency : BaseEntity
{
    public required string Code { get; set; }
    public required string Symbol { get; set; }
    public required string Name { get; set; }
}