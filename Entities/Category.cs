namespace WalletApp.Entities;

public class Category : BaseEntity
{
    public required string Name { get; set; }
    public string? Icon { get; set; }

    public Guid? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }
}