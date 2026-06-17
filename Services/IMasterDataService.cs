using WalletApp.Entities;

namespace WalletApp.Services;

public interface IMasterDataService
{
    // Categories
    Task<IEnumerable<Category>> GetCategoriesAsync();
    Task<Category> GetCategoryByIdAsync(Guid id);
    Task<Category> CreateCategoryAsync(Category category);
    Task<Category> UpdateCategoryAsync(Guid id, Category category);
    Task DeleteCategoryAsync(Guid id);
    
    // Merchants
    Task<IEnumerable<Merchant>> GetMerchantsAsync();
    Task<Merchant> GetMerchantByIdAsync(Guid id);
    Task<Merchant> CreateMerchantAsync(Merchant merchant);
    Task<Merchant> UpdateMerchantAsync(Guid id, Merchant merchant);
    Task DeleteMerchantAsync(Guid id);

    // Countries
    Task<IEnumerable<Country>> GetCountriesAsync();
    Task<Country> GetCountryByIdAsync(Guid id);
    Task<Country> CreateCountryAsync(Country country);
    Task<Country> UpdateCountryAsync(Guid id, Country country);
    Task DeleteCountryAsync(Guid id);

    // Currencies
    Task<IEnumerable<Currency>> GetCurrenciesAsync();
    Task<Currency> GetCurrencyByIdAsync(Guid id);
    Task<Currency> CreateCurrencyAsync(Currency currency);
    Task<Currency> UpdateCurrencyAsync(Guid id, Currency currency);
    Task DeleteCurrencyAsync(Guid id);
}