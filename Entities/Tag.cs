namespace WalletApp.Entities;

public class Tag : BaseEntity
{
    public required string Name { get; set; }
    public string? Color { get; set; }

    public ICollection<TransactionTag> TransactionTags { get; set; } = new List<TransactionTag>();
}