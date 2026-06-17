using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using WalletApp.Data;
using WalletApp.Entities;

namespace WalletApp.Services;

public class MasterDataService : IMasterDataService
{
    private readonly AppDbContext _context;
    private readonly IDistributedCache _cache;
    private const string CategoriesCacheKey = "all_categories";
    private const string MerchantsCacheKey = "all_merchants";
    private const string CountriesCacheKey = "all_countries";
    private const string CurrenciesCacheKey = "all_currencies";

    public MasterDataService(AppDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    ///////// CATEGORIES /////////
    public async Task<IEnumerable<Category>> GetCategoriesAsync()
    {
        // Check if the box is inside RAM
        var cachedData = await _cache.GetStringAsync(CategoriesCacheKey);
        if (!string.IsNullOrEmpty(cachedData))
        {
            // Found the box. Convert to c# object and return.
            return JsonSerializer.Deserialize<IEnumerable<Category>>(cachedData) ?? Enumerable.Empty<Category>();
        }

        // If not inside RAM, go to DB
        var categories = await _context.Categories
            .Include(c => c.ParentCategory)
            .ToListAsync();
        
        // Convert to JSON string for further requests
        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        var serilizedData = JsonSerializer.Serialize(categories);
        await _cache.SetStringAsync(CategoriesCacheKey, serilizedData, cacheOptions);

        return categories;
    }

    public async Task<Category> GetCategoryByIdAsync(Guid id)
    {
        var category = await _context.Categories
            .Include(c => c.ParentCategory)
            .FirstOrDefaultAsync(c => c.Id == id);
        if(category is null) throw new KeyNotFoundException($"Category not found. ID: {id}");
        return category;
    }

    public async Task<Category> CreateCategoryAsync(Category category)
    {
        var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == category.Name.ToLower());
        if(exists) throw new ArgumentException($"'{category.Name}' already exists.");

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        // Cache invalidation
        await _cache.RemoveAsync(CategoriesCacheKey);

        return category;
    }

    public async Task<Category> UpdateCategoryAsync(Guid id, Category updatedCategory)
    {
        var category = await _context.Categories.FindAsync(id);
        if(category is null) throw new KeyNotFoundException($"Category not found. ID: {id}");

        // if name changed, check if it's already in use
        if(category.Name.ToLower() != updatedCategory.Name.ToLower())
        {
            var exists = await _context.Categories.AnyAsync(c => c.Name.ToLower() == updatedCategory.Name.ToLower() && c.Id != id);
            if(exists) throw new ArgumentException($"'{updatedCategory.Name}' already exists.");
        }

        category.Name = updatedCategory.Name;
        category.Icon = updatedCategory.Icon;
        category.ParentCategoryId = updatedCategory.ParentCategoryId;

        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(CategoriesCacheKey);

        return category;
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        bool hasTransactions = await _context.Transactions.AnyAsync(t => t.CategoryId == id);
        if(hasTransactions)
            throw new InvalidOperationException("There are transactions tied to this category. Move them to another category before delete.");
        
        bool hasChildCategories = await _context.Categories.AnyAsync(c => c.ParentCategoryId == id);
        if(hasChildCategories)
            throw new InvalidOperationException("This category has sub-categories! Delete or update them first.");

        bool isDefaultMerchantCategory = await _context.Merchants.AnyAsync(m => m.DefaultCategoryId == id);
        if(isDefaultMerchantCategory)
            throw new InvalidOperationException("This category has been set to be default for some merchants. Update them first.");

        var category = await _context.Categories.FindAsync(id);
        if(category is null) throw new KeyNotFoundException($"Category not found. ID: {id}");
        
        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        // Cache invalidation
        await _cache.RemoveAsync(CategoriesCacheKey);
    }

    ///////// MERCHANTS /////////
    public async Task<IEnumerable<Merchant>> GetMerchantsAsync()
    {
        var cachedData = await _cache.GetStringAsync(MerchantsCacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<IEnumerable<Merchant>>(cachedData) ?? Enumerable.Empty<Merchant>();
        }

        var merchants = await _context.Merchants
            .Include(m => m.DefaultCategory)
            .ToListAsync();

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        var serilizedData = JsonSerializer.Serialize(merchants);
        await _cache.SetStringAsync(MerchantsCacheKey, serilizedData, cacheOptions);

        return merchants;
    }

    public async Task<Merchant> GetMerchantByIdAsync(Guid id)
    {
        var merchant = await _context.Merchants
            .Include(m => m.DefaultCategory)
            .FirstOrDefaultAsync(m => m.Id == id);
        
        if(merchant is null) throw new KeyNotFoundException($"Merchant not found. ID: {id}");
        return merchant;
    }

    public async Task<Merchant> CreateMerchantAsync(Merchant merchant)
    {
        var exists = await _context.Merchants.AnyAsync(m => m.Name.ToLower() == merchant.Name.ToLower());
        if(exists) throw new ArgumentException($"'{merchant.Name}' already exists.");

        _context.Merchants.Add(merchant);
        await _context.SaveChangesAsync();

        await _cache.RemoveAsync(MerchantsCacheKey);

        return merchant;
    }

    public async Task<Merchant> UpdateMerchantAsync(Guid id, Merchant updatedMerchant)
    {
        var merchant = await _context.Merchants.FindAsync(id);
        if(merchant is null) throw new KeyNotFoundException($"Merchant not found. ID: {id}");

        if(merchant.Name.ToLower() != updatedMerchant.Name.ToLower())
        {
            var exists = await _context.Merchants.AnyAsync(m => m.Name.ToLower() == updatedMerchant.Name.ToLower() && m.Id != id);
            if(exists) throw new ArgumentException($"'{updatedMerchant.Name}' already exists.");
        }

        merchant.Name = updatedMerchant.Name;
        merchant.DefaultCategoryId = updatedMerchant.DefaultCategoryId;

        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(MerchantsCacheKey);

        return merchant;
    }

    public async Task DeleteMerchantAsync(Guid id)
    {
        bool isInUse = await _context.Transactions.AnyAsync(t => t.MerchantId == id);
        if(isInUse) throw new InvalidOperationException("There are transactions tied to this merchant. Clear them before delete.");
        
        var merchant = await _context.Merchants.FindAsync(id);
        if(merchant is null) throw new KeyNotFoundException($"Merchant not found. ID: {id}");

        _context.Merchants.Remove(merchant);
        await _context.SaveChangesAsync();

        await _cache.RemoveAsync(MerchantsCacheKey);
    }

    ///////// COUNTRIES /////////
    public async Task<IEnumerable<Country>> GetCountriesAsync()
    {
        var cachedData = await _cache.GetStringAsync(CountriesCacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<IEnumerable<Country>>(cachedData) ?? Enumerable.Empty<Country>();
        }
        var countries = await _context.Countries.ToListAsync();

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        var serilizedData = JsonSerializer.Serialize(countries);
        await _cache.SetStringAsync(CountriesCacheKey, serilizedData, cacheOptions);

        return countries;
    }

    public async Task<Country> GetCountryByIdAsync(Guid id)
    {
        var country = await _context.Countries.FindAsync(id);
        if(country is null) throw new KeyNotFoundException($"Country not found. ID: {id}");
        return country;
    }

    public async Task<Country> CreateCountryAsync(Country country)
    {
        var exists = await _context.Countries.AnyAsync(c=> c.Name.ToLower() == country.Name.ToLower());
        if(exists) throw new ArgumentException($"'{country.Name}' already exists.");
        
        _context.Countries.Add(country);
        await _context.SaveChangesAsync();

        await _cache.RemoveAsync(CountriesCacheKey);

        return country;
    }

    public async Task<Country> UpdateCountryAsync(Guid id, Country updatedCountry)
    {
        var country = await _context.Countries.FindAsync(id);
        if(country is null) throw new KeyNotFoundException($"Country not found. ID: {id}");

        var newNameLower = updatedCountry.Name.ToLower();
        var newCodeLower = updatedCountry.Code.ToLower();

        if(country.Name.ToLower() != newNameLower || country.Code.ToLower() != newCodeLower)
        {
            var exists = await _context.Countries.AnyAsync(c =>
                (c.Name.ToLower() == newNameLower || c.Code.ToLower() == newCodeLower) && c.Id != id);
            
            if(exists) throw new ArgumentException($"Country name or code is already in use.");
        }

        country.Name = updatedCountry.Name;
        country.Code = updatedCountry.Code.ToUpper();

        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(CountriesCacheKey);

        return country;
    }

    public async Task DeleteCountryAsync(Guid id)
    {
        bool isInUse = await _context.Transactions.AnyAsync(t => t.CountryId == id);
        if(isInUse) throw new InvalidOperationException("There are transactions tied to this country. Update them before delete.");
        
        var country = await _context.Countries.FindAsync(id);
        if(country is null) throw new KeyNotFoundException($"Country not found. ID: {id}");

        _context.Countries.Remove(country);
        await _context.SaveChangesAsync();

        await _cache.RemoveAsync(CountriesCacheKey);
    }

    ///////// CURRENCIES /////////
    public async Task<IEnumerable<Currency>> GetCurrenciesAsync()
    {
        var cachedData = await _cache.GetStringAsync(CurrenciesCacheKey);

        if (!string.IsNullOrEmpty(cachedData))
        {
            return JsonSerializer.Deserialize<IEnumerable<Currency>>(cachedData) ?? Enumerable.Empty<Currency>();
        }
        var currencies = await _context.Currencies.ToListAsync();

        var cacheOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
        };
        var serilizedData = JsonSerializer.Serialize(currencies);
        await _cache.SetStringAsync(CurrenciesCacheKey, serilizedData, cacheOptions);

        return currencies;
    }

    public async Task<Currency> GetCurrencyByIdAsync(Guid id)
    {
        var currency = await _context.Currencies.FindAsync(id);
        if(currency is null) throw new KeyNotFoundException($"Currency not found. ID: {id}");
        return currency;
    }

    public async Task<Currency> CreateCurrencyAsync(Currency currency)
    {
        currency.Code = currency.Code.ToUpper();
        var exists = await _context.Currencies.AnyAsync(c=> c.Code == currency.Code);
        if(exists) throw new ArgumentException($"'{currency.Code}' already exists.");

        _context.Currencies.Add(currency);
        await _context.SaveChangesAsync();

        await _cache.RemoveAsync(CurrenciesCacheKey);

        return currency;
    }

    public async Task<Currency> UpdateCurrencyAsync(Guid id, Currency updatedCurrency)
    {
        var currency = await _context.Currencies.FindAsync(id);
        if(currency is null) throw new KeyNotFoundException($"Currency not found. ID: {id}");

        var newCodeUpper = updatedCurrency.Code.ToUpper();

        if(currency.Code.ToUpper() != newCodeUpper)
        {
            var exists = await _context.Currencies.AnyAsync(c => c.Code.ToUpper() == newCodeUpper && c.Id != id);
            if(exists) throw new ArgumentException($"'{newCodeUpper}' already exists.");
        }

        currency.Name = updatedCurrency.Name;
        currency.Code = newCodeUpper;
        currency.Symbol = updatedCurrency.Symbol;

        await _context.SaveChangesAsync();
        await _cache.RemoveAsync(CurrenciesCacheKey);

        return currency;
    }

    public async Task DeleteCurrencyAsync(Guid id)
    {
        bool isInUse = await _context.Transactions.AnyAsync(t => t.CurrencyId == id);
        if(isInUse) throw new InvalidOperationException("There are transactions with this currency. Update them first.");
        
        var currency = await _context.Currencies.FindAsync(id);
        if(currency is null) throw new KeyNotFoundException($"Currency not found. ID: {id}");

        _context.Currencies.Remove(currency);
        await _context.SaveChangesAsync();

        await _cache.RemoveAsync(CurrenciesCacheKey);
    }
}