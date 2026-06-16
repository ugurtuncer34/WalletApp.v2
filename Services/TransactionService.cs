using Microsoft.EntityFrameworkCore;
using System.Data;
using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using WalletApp.Data;
using WalletApp.Dtos;
using WalletApp.Entities;

namespace WalletApp.Services;

public class TransactionService : ITransactionService
{
    private readonly AppDbContext _context;
    private readonly ILogger<TransactionService> _logger;
    private readonly CultureInfo _trCulture = new CultureInfo("tr-TR");

    public TransactionService(AppDbContext context, ILogger<TransactionService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<PagedResult<TransactionResponse>> GetTransactionsAsync(TransactionQueryParameters queryParams)
    {
        var query = _context.Transactions.AsQueryable();

        if (queryParams.CategoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == queryParams.CategoryId.Value);
        }

        if (queryParams.MerchantId.HasValue)
        {
            query = query.Where(t => t.MerchantId == queryParams.MerchantId.Value);
        }

        if (queryParams.StartDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate >= queryParams.StartDate.Value);
        }

        if (queryParams.EndDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate < queryParams.EndDate.Value);
        }

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.TransactionDate)
            .Skip((queryParams.PageNumber - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
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

        return new PagedResult<TransactionResponse>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = queryParams.PageNumber,
            PageSize = queryParams.PageSize
        };
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

        if (transaction is null)
            throw new KeyNotFoundException($"Transaction not found. ID: {id}");

        return transaction;
    }

    public async Task<Transaction> CreateTransactionAsync(CreateTransactionRequest request)
    {
        // Category (Mandatory)
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

        // Merchant (Optional)
        Guid? finalMerchantId = null;
        if (request.MerchantId.HasValue && request.MerchantId.Value != Guid.Empty)
        {
            var merchantExists = await _context.Merchants.AnyAsync(m => m.Id == request.MerchantId.Value);

            if (!merchantExists)
            {
                throw new ArgumentException($"Invalid Merchant ID: {request.MerchantId.Value}");
            }

            finalMerchantId = request.MerchantId.Value;
        }

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = request.Date ?? DateTime.UtcNow,
            Amount = request.Amount,
            Description = request.Description,
            CategoryId = finalCategoryId,
            MerchantId = finalMerchantId
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

        // Amount
        if (!decimal.TryParse(parts[0], out decimal amount))
        {
            throw new ArgumentException("İlk kelime geçerli bir tutar olmalıdır. Örn: '150 Market'");
        }

        var rawText = string.Join(" ", parts.Skip(1)).Trim();
        var processingText = rawText;
        var processingTextLower = processingText.ToLower(_trCulture);

        // Merchant
        var allMerchants = await _context.Merchants.Include(m => m.DefaultCategory).ToListAsync();

        Merchant? matchedMerchant = null;
        int earliestIndex = int.MaxValue;
        int matchedLength = -1;

        foreach (var merchant in allMerchants)
        {
            var merchantNameLower = merchant.Name.ToLower(_trCulture);
            int index = processingTextLower.IndexOf(merchantNameLower, StringComparison.Ordinal);

            // if merchant inside text
            if (index >= 0)
            {
                // if inside text earlier or starting at the same place but longer
                if (index < earliestIndex || (index == earliestIndex && merchant.Name.Length > matchedLength))
                {
                    earliestIndex = index;
                    matchedLength = merchant.Name.Length;
                    matchedMerchant = merchant;
                }
            }
        }

        Category? targetCategory = null;

        if (matchedMerchant != null)
        {
            targetCategory = matchedMerchant.DefaultCategory;

            processingText = processingText.Remove(earliestIndex, matchedLength);
        }
        else
        {
            try
            {
                var rulesFilePath = "category-rules.json";
                if (!File.Exists(rulesFilePath)) rulesFilePath = "category-rules.example.json";

                if (File.Exists(rulesFilePath))
                {
                    var jsonContent = await File.ReadAllTextAsync(rulesFilePath);
                    var categoryRules = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(jsonContent);

                    if (categoryRules != null)
                    {
                        var currentTextLower = processingText.ToLower(_trCulture);

                        foreach (var rule in categoryRules)
                        {
                            if (rule.Value.Any(keyword => currentTextLower.Contains(keyword.ToLower(_trCulture), StringComparison.Ordinal)))
                            {
                                var ruleKeyUpper = rule.Key.ToUpperInvariant();
                                targetCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToUpper() == ruleKeyUpper);
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Rule engine error. Saving to the category 'Other'");
            }
        }

        if (targetCategory == null)
        {
            targetCategory = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToUpper() == "DİĞER");
        }
        if (targetCategory == null) throw new ArgumentException("There is no category named 'OTHER' in the system");

        // Clear the description
        processingText = Regex.Replace(processingText, @"\s+", " ").Trim();

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = DateTime.UtcNow,
            Amount = amount,
            Description = string.IsNullOrWhiteSpace(processingText) ? null : processingText,
            CategoryId = targetCategory.Id,
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
            CategoryName = targetCategory.Name,
            CategoryIcon = targetCategory.Icon,
            MerchantName = matchedMerchant?.Name ?? string.Empty
        };

        ////////////// ==== OLD ALGORITHM === //////////////
        // var description = parts.Length > 1 ? string.Join(" ", parts.Skip(1)).Trim() : "Belirtilmedi.";
        // var descLower = description.ToLower(_trCulture);

        // string targetCategoryName = "DİĞER";

        // // Dynamic reading of rule engine json
        // try
        // {
        //     var rulesFilePath = "category-rules.json";
        //     if (!File.Exists(rulesFilePath))
        //     {
        //         rulesFilePath = "category-rules.example.json";
        //     }

        //     if (File.Exists(rulesFilePath))
        //     {
        //         var jsonContent = await File.ReadAllTextAsync(rulesFilePath);

        //         // Convert json to dictionary: Key = categoryName, Val = wordsArray
        //         var categoryRules = JsonSerializer.Deserialize<Dictionary<string, List<string>>>(jsonContent);

        //         if(categoryRules != null)
        //         {
        //             foreach(var rule in categoryRules)
        //             {
        //                 if(rule.Value.Any(keyword => descLower.Contains(keyword.ToLower(_trCulture))))
        //                 {
        //                     targetCategoryName = rule.Key;
        //                     break;
        //                 }
        //             }
        //         }
        //     }
        // }
        // catch (Exception ex)
        // {
        //    Console.WriteLine($"Rule engine error: {ex.Message}");
        // }

        // var category = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToUpper() == targetCategoryName);
        // if (category is null)
        // {
        //     category = await _context.Categories.FirstOrDefaultAsync(c => c.Name.ToUpper() == "DİĞER");
        // }
        // if (category is null)
        //     throw new ArgumentException("There is no category in the system.");

        // var allMerchants = await _context.Merchants.ToListAsync();
        // var matchedMerchant = allMerchants.FirstOrDefault(m => descLower.Contains(m.Name.ToLower(_trCulture)));

        // var transaction = new Transaction
        // {
        //     Id = Guid.NewGuid(),
        //     TransactionDate = DateTime.UtcNow,
        //     Amount = amount,
        //     Description = description,
        //     CategoryId = category.Id,
        //     MerchantId = matchedMerchant?.Id
        // };

        // _context.Transactions.Add(transaction);
        // await _context.SaveChangesAsync();

        // return new TransactionResponse
        // {
        //     Id = transaction.Id,
        //     Date = transaction.TransactionDate,
        //     Amount = transaction.Amount,
        //     Description = transaction.Description,
        //     CategoryName = category.Name,
        //     CategoryIcon = category.Icon,
        //     MerchantName = matchedMerchant != null ? matchedMerchant.Name : string.Empty
        // };
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