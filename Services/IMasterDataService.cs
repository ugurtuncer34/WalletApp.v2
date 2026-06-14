using WalletApp.Entities;

namespace WalletApp.Services;

public interface IMasterDataService
{
    // Categories
    Task<IEnumerable<Category>> GetCategoriesAsync();
    Task<Category> GetCategoryByIdAsync(Guid id);
    Task<Category> CreateCategoryAsync(Category category);
    Task DeleteCategoryAsync(Guid id);
}