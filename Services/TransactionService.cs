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

        if (queryParams.CountryId.HasValue)
        {
            query = query.Where(t => t.CountryId == queryParams.CountryId.Value);
        }

        if (queryParams.CurrencyId.HasValue)
        {
            query = query.Where(t => t.CurrencyId == queryParams.CurrencyId.Value);
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
                ExchangeRate = t.ExchangeRate,
                CategoryName = t.Category.Name,
                CategoryIcon = t.Category.Icon,
                MerchantName = t.Merchant != null ? t.Merchant.Name : string.Empty,
                CountryName = t.Country != null ? t.Country.Name : string.Empty,
                CurrencySymbol = t.Currency != null ? t.Currency.Symbol : string.Empty
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
                ExchangeRate = t.ExchangeRate,
                CategoryName = t.Category.Name,
                CategoryIcon = t.Category.Icon,
                MerchantName = t.Merchant != null ? t.Merchant.Name : string.Empty,
                CountryName = t.Country != null ? t.Country.Name : string.Empty,
                CurrencySymbol = t.Currency != null ? t.Currency.Symbol : string.Empty
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

        // Country (Optional, default TR)
        Guid finalCountryId;
        if(request.CountryId == null || request.CountryId == Guid.Empty)
        {
            var defaultCountry = await _context.Countries.FirstOrDefaultAsync(c => c.Code.ToUpper() == "TR");
            if(defaultCountry is null) throw new ArgumentException("Default country (TR) not found in database.");

            finalCountryId = defaultCountry.Id;
        }
        else
        {
            finalCountryId = request.CountryId.Value;
            if(!await _context.Countries.AnyAsync(c => c.Id == finalCountryId))
                throw new ArgumentException($"Invalid Country ID: {finalCountryId}");
        }

        // Currency
        Guid finalCurrencyId;
        if(request.CurrencyId == null || request.CurrencyId == Guid.Empty)
        {
            var defaultCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code.ToUpper() == "TRY");
            if(defaultCurrency is null) throw new ArgumentException("Default currency (TRY) not found in database.");

            finalCurrencyId = defaultCurrency.Id;
        }
        else
        {
            finalCurrencyId = request.CurrencyId.Value;
            if(!await _context.Currencies.AnyAsync(c => c.Id == finalCurrencyId))
                throw new ArgumentException($"Invalid Country ID: {finalCurrencyId}");
        }

        // ExchangeRate
        var finalExchangeRate = request.ExchangeRate ?? 1m;

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = request.Date ?? DateTime.UtcNow,
            Amount = request.Amount,
            Description = request.Description,
            ExchangeRate = finalExchangeRate,
            CategoryId = finalCategoryId,
            MerchantId = finalMerchantId,
            CountryId = finalCountryId,
            CurrencyId = finalCurrencyId
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

        // Default settings
        var defaultCountry = await _context.Countries.FirstOrDefaultAsync(c => c.Code.ToUpper() == "TR");
        var defaultCurrency = await _context.Currencies.FirstOrDefaultAsync(c => c.Code.ToUpper() == "TRY");

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = DateTime.UtcNow,
            Amount = amount,
            Description = string.IsNullOrWhiteSpace(processingText) ? null : processingText,
            ExchangeRate = 1m,
            CategoryId = targetCategory.Id,
            MerchantId = matchedMerchant?.Id,
            CountryId = defaultCountry?.Id,
            CurrencyId = defaultCurrency?.Id
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return new TransactionResponse
        {
            Id = transaction.Id,
            Date = transaction.TransactionDate,
            Amount = transaction.Amount,
            Description = transaction.Description,
            ExchangeRate = 1m,
            CategoryName = targetCategory.Name,
            CategoryIcon = targetCategory.Icon,
            MerchantName = matchedMerchant?.Name ?? string.Empty,
            CountryName = defaultCountry?.Name ?? string.Empty,
            CurrencySymbol = defaultCurrency?.Symbol ?? string.Empty
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