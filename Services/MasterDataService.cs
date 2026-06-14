using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Entities;

namespace WalletApp.Services;

public class MasterDataService : IMasterDataService
{
    private readonly AppDbContext _context;

    public MasterDataService(AppDbContext context)
    {
        _context = context;
    }

    // CATEGORIES
    public async Task<IEnumerable<Category>> GetCategoriesAsync()
    {
        return await _context.Categories
            .Include(c => c.ParentCategory)
            .ToListAsync();
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
        return category;
    }

    public async Task DeleteCategoryAsync(Guid id)
    {
        var category = await _context.Categories.FindAsync(id);
        if(category is null) throw new KeyNotFoundException($"Category not found. ID: {id}");
        
        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();
    }

    // MERCHANTS
    public async Task<IEnumerable<Merchant>> GetMerchantsAsync()
    {
        return await _context.Merchants
            .Include(m => m.DefaultCategory)
            .ToListAsync();
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
        return merchant;
    }

    public async Task DeleteMerchantAsync(Guid id)
    {
        var merchant = await _context.Merchants.FindAsync(id);
        if(merchant is null) throw new KeyNotFoundException($"Merchant not found. ID: {id}");

        _context.Merchants.Remove(merchant);
        await _context.SaveChangesAsync();
    }
}