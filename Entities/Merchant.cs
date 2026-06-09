namespace WalletApp.Entities;

public class Merchant : BaseEntity
{
    public string Name { get; set; } = null!;
    // Auto category
    public Guid? DefaultCategoryId { get; set; }
    public Category? DefaultCategory { get; set; }
}