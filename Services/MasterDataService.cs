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

    public MasterDataService(AppDbContext context, IDistributedCache cache)
    {
        _context = context;
        _cache = cache;
    }

    // CATEGORIES
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
            .FirstOrDefaultAsync();
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

    public async Task DeleteCategoryAsync(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if(category is null) throw new KeyNotFoundException($"Category not found. ID: {id}");
        
        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        // Cache invalidation
        await _cache.RemoveAsync(CategoriesCacheKey);
    }

    // MERCHANTS
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
            .FirstOrDefaultAsync();
        
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

    public async Task DeleteMerchantAsync(Guid id)
    {
        var merchant = await _context.Merchants.FindAsync(id);
        if(merchant is null) throw new KeyNotFoundException($"Merchant not found. ID: {id}");

        _context.Merchants.Remove(merchant);
        await _context.SaveChangesAsync();

        await _cache.RemoveAsync(MerchantsCacheKey);
    }
}