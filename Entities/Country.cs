namespace WalletApp.Entities;

public class Country : BaseEntity
{
    public required string Name { get; set; }
    public string Code { get; set; } = null!;
}