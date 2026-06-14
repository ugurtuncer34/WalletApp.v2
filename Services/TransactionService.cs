using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Text.Json;
using WalletApp.Data;
using WalletApp.Dtos;
using WalletApp.Entities;

namespace WalletApp.Services;

public class TransactionService : ITransactionService
{
    private readonly AppDbContext _context;
    private readonly CultureInfo _trCulture = new CultureInfo("tr-TR");

    public TransactionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<TransactionResponse>> GetTransactionsAsync()
    {
        return await _context.Transactions
            .OrderByDescending(t => t.TransactionDate)
            .Select(t => new TransactionResponse
            {
                Id = t.Id,
                Date = t.TransactionDate,
                Amount = t.Amount,
                Description = t.Description,
                CategoryName = t.Category.Name,
                CategoryIcon = t.Category.Icon,
                MerchantName = t.Merchant != null ? t.Merchant.Name : string.Empty
            })
            .ToListAsync();
    }

    public async Task<TransactionResponse> GetTransactionByIdAsync(Guid id)
    {
        var transaction = await _context.Transactions
            .Where(t => t.Id == id)
            .Select(t => new TransactionResponse
            {
                Id = t.Id,
                Date = t.TransactionDate,
                Amount = t.Amount,
                Description = t.Description,
                CategoryName = t.Category.Name,
                CategoryIcon = t.Category.Icon,
                MerchantName = t.Merchant != null ? t.Merchant.Name : string.Empty
            })
            .FirstOrDefaultAsync();

        if(transaction is null)
            throw new KeyNotFoundException($"Transaction not found. ID: {id}");
        
        return transaction;
    }

    public async Task<Transaction> CreateTransactionAsync(CreateTransactionRequest request)
    {
        Guid finalCategoryId;
        if (request.CategoryId == null || request.CategoryId == Guid.Empty)
        {
            var defaultCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name == "DİĞER");
            
            if (defaultCategory == null)
                throw new ArgumentException("Default category (OTHER) not found in database.");
            
            finalCategoryId = defaultCategory.Id;
        }
        else
        {
            finalCategoryId = request.CategoryId.Value;
        }

        if (!await _context.Categories.AnyAsync(c => c.Id == finalCategoryId))
        {
            throw new ArgumentException($"Invalid Category ID: {finalCategoryId}");
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = request.Date ?? DateTime.UtcNow,
            Amount = request.Amount,
            Description = request.Description,
            CategoryId = finalCategoryId
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    public async Task<TransactionResponse> QuickAddTransactionAsync(QuickAddRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text))
        {
            throw new ArgumentException("Lütfen bir harcama metni girin. Örn: '150 Market'");
        }

        var parts = request.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        
        if (parts.Length == 0)
            throw new ArgumentException("Lütfen geçerli bir metin girin.");

        if (!decimal.TryParse(parts[0], out decimal amount))
        {
            throw new ArgumentException("İlk kelime geçerli bir tutar olmalıdır. Örn: '150 Market'");
        }

        var description = parts.Length > 1 ? string.Join(" ", parts.Skip(1)).Trim() : "Belirtilmedi.";
        var descLower = description.ToLower(_trCulture);

        string targetCategoryName = "DİĞER";

        // Dynamic reading of rule engine json
        try
        {
            var rulesFilePath = "category-rules.json";
            if (!File.Exists(rulesFilePath))
            {
                rulesFilePath = "category-rules.example.json";
            }

            if (File.Exists(rulesFilePath))
            {
                var jsonContent = await File.ReadAllTextAsync(rulesFilePath);

                // Convert json to dictionary: Key = categoryName, Val = wordsArray
                var categoryRules = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(jsonContent);

                if(categoryRules != null)
                {
                    foreach(var rule in categoryRules)
                    {
                        if(rule.Value.Any(keyword => descLower.Contains(keyword.ToLower(_trCulture))))
                        {
                            targetCategoryName = rule.Key;
                            break;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
           Console.WriteLine($"Rule engine error: {ex.Message}");
        }

        var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToUpper() == targetCategoryName);
        if (category is null)
        {
            category = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToUpper() == "DİĞER");
        }
        if (category is null)
            throw new ArgumentException("There is no category in the system.");

        var allMerchants = await _context.Merchants.ToListAsync();
        var matchedMerchant = allMerchants.FirstOrDefault(m => descLower.Contains(m.Name.ToLower(_trCulture)));

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = DateTime.UtcNow,
            Amount = amount,
            Description = description,
            CategoryId = category.Id,
            MerchantId = matchedMerchant?.Id
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return new TransactionResponse
        {
            Id = transaction.Id,
            Date = transaction.TransactionDate,
            Amount = transaction.Amount,
            Description = transaction.Description,
            CategoryName = category.Name,
            CategoryIcon = category.Icon,
            MerchantName = matchedMerchant != null ? matchedMerchant.Name : string.Empty
        };
    }

    public async Task DeleteTransactionAsync(Guid id)
    {
        var transaction = await _context.Transactions.FindAsync(id);
        if (transaction is null)
            throw new KeyNotFoundException($"Transaction not found. ID: {id}");

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();
    }
}