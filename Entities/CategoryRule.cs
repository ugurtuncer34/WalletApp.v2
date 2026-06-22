namespace WalletApp.Entities;

public class CategoryRule : BaseEntity
{
    public string Keyword { get; set; } = string.Empty;
    // Navigation to belonging category
    public Guid CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}