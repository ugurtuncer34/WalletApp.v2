using WalletApp.Entities;

namespace WalletApp.Services;

public interface IMasterDataService
{
    // Categories
    Task<IEnumerable<Category>> GetCategoriesAsync();
    Task<Category> GetCategoryByIdAsync(Guid id);
    Task<Category> CreateCategoryAsync(Category category);
    Task DeleteCategoryAsync(Guid id);
    
    // Merchants
    Task<IEnumerable<Merchant>> GetMerchantsAsync();
    Task<Merchant> GetMerchantByIdAsync(Guid id);
    Task<Merchant> CreateMerchantAsync(Merchant merchant);
    Task DeleteMerchantAsync(Guid id);
}