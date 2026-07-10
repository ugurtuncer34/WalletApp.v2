using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Data;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using WalletApp.Data;
using WalletApp.Dtos;
using WalletApp.Entities;

namespace WalletApp.Services;

public class TransactionService : ITransactionService
{
    private readonly AppDbContext _context;
    private readonly IMasterDataService _masterDataService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IExchangeRateService _exchangeRateService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly CultureInfo _trCulture = new CultureInfo("tr-TR");

    public TransactionService(
        AppDbContext context, 
        IMasterDataService masterDataService, 
        ICurrentUserService currentUserService,
        IExchangeRateService exchangeRateService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration
    )
    {
        _context = context;
        _masterDataService = masterDataService;
        _currentUserService = currentUserService;
        _exchangeRateService = exchangeRateService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<PagedResult<TransactionResponse>> GetTransactionsAsync(TransactionQueryParameters queryParams)
    {
        var query = _context.Transactions.AsQueryable();

        //// No need for below query because all family member can see the family finance ////
        // var query = _context.Transactions
        //     .Where(t => t.UserId == _currentUserService.UserId)
        //     .AsQueryable();

        if (queryParams.CategoryId.HasValue)
        {
            var targetCatId = queryParams.CategoryId.Value;

            var allCategories = await _masterDataService.GetCategoriesAsync();
            var childIds = allCategories
                .Where(c => c.ParentCategory?.Id == targetCatId)
                .Select(c => c.Id)
                .ToList();

            if (childIds.Any())
            {
                childIds.Add(targetCatId);
                query = query.Where(t => childIds.Contains(t.CategoryId));
            }
            else
            {
                query = query.Where(t => t.CategoryId == targetCatId);
            }
        }

        if (queryParams.MerchantId.HasValue)
        {
            query = query.Where(t => t.MerchantId == queryParams.MerchantId.Value);
        }

        if (queryParams.StartDate.HasValue)
        {
            var start = queryParams.StartDate.Value;
            queryParams.StartDate = start.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(start, DateTimeKind.Utc)
                : start.ToUniversalTime();

            query = query.Where(t => t.TransactionDate >= queryParams.StartDate.Value);
        }

        if (queryParams.EndDate.HasValue)
        {
            var end = queryParams.EndDate.Value;
            queryParams.EndDate = end.Kind == DateTimeKind.Unspecified 
                ? DateTime.SpecifyKind(end, DateTimeKind.Utc) 
                : end.ToUniversalTime();

            query = query.Where(t => t.TransactionDate < queryParams.EndDate.Value.AddDays(1));
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
                CurrencySymbol = t.Currency != null ? t.Currency.Symbol : string.Empty,
                AddedBy = t.User.Username
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
                CurrencySymbol = t.Currency != null ? t.Currency.Symbol : string.Empty,
                AddedBy = t.User.Username
            })
            .FirstOrDefaultAsync();

        if (transaction is null)
            throw new KeyNotFoundException($"Transaction not found. ID: {id}");

        return transaction;
    }

    public async Task<TransactionResponse> CreateTransactionAsync(CreateTransactionRequest request)
    {
        // Category (Mandatory)
        var allCategories = await _masterDataService.GetCategoriesAsync(); // from cache
        CategoryResponseDto? targetCategory;

        if (request.CategoryId == null || request.CategoryId == Guid.Empty) // if(request.CategoryId.GetValueOrDefault() == Guid.Empty)
        {
            targetCategory = allCategories.FirstOrDefault(c => c.Name == "DİĞER");
            if (targetCategory == null) throw new ArgumentException("Default category (OTHER) not found in database.");
        }
        else
        {
            targetCategory = allCategories.FirstOrDefault(c => c.Id == request.CategoryId.Value);
            if (targetCategory == null) throw new ArgumentException($"Invalid Category ID: {request.CategoryId.Value}");
        }

        // Merchant (Optional)
        MerchantResponseDto? targetMerchant = null;
        if (request.MerchantId.HasValue && request.MerchantId.Value != Guid.Empty)
        {
            var allMerchants = await _masterDataService.GetMerchantsAsync(); // from cache
            targetMerchant = allMerchants.FirstOrDefault(m => m.Id == request.MerchantId.Value);

            if (targetMerchant == null) throw new ArgumentException($"Invalid Merchant ID: {request.MerchantId.Value}");
        }

        // Country (Optional, default TR)
        var allCountries = await _masterDataService.GetCountriesAsync(); // from cache
        Country? targetCountry;
        if (request.CountryId == null || request.CountryId == Guid.Empty)
        {
            targetCountry = allCountries.FirstOrDefault(c => c.Code.ToUpper() == "TR");
            if (targetCountry is null) throw new ArgumentException("Default country (TR) not found in database.");
        }
        else
        {
            targetCountry = allCountries.FirstOrDefault(c => c.Id == request.CountryId.Value);
            if (targetCountry is null) throw new ArgumentException($"Invalid Country ID: {request.CountryId.Value}");
        }

        // Currency (Optional, default TRY)
        var allCurrencies = await _masterDataService.GetCurrenciesAsync(); // from cache
        Currency? targetCurrency;
        if (request.CurrencyId == null || request.CurrencyId == Guid.Empty)
        {
            targetCurrency = allCurrencies.FirstOrDefault(c => c.Code.ToUpper() == "TRY");
            if (targetCurrency is null) throw new ArgumentException("Default currency (TRY) not found in database.");
        }
        else
        {
            targetCurrency = allCurrencies.FirstOrDefault(c => c.Id == request.CurrencyId.Value);
            if (targetCurrency is null) throw new ArgumentException($"Invalid Currency ID: {request.CurrencyId.Value}");
        }

        // ExchangeRate
        var transactionDate = request.Date ?? DateTime.UtcNow;
        var finalExchangeRate = await _exchangeRateService.GetExchangeRateAsync(targetCurrency.Code, transactionDate);

        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            TransactionDate = request.Date ?? DateTime.UtcNow,
            Amount = request.Amount,
            Description = request.Description,
            ExchangeRate = finalExchangeRate, // retrieved from Go service, live or past rate depending on date
            CategoryId = targetCategory.Id,
            MerchantId = targetMerchant?.Id,
            CountryId = targetCountry.Id,
            CurrencyId = targetCurrency.Id,
            UserId = _currentUserService.UserId
        };

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return new TransactionResponse
        {
            Id = transaction.Id,
            Date = transaction.TransactionDate,
            Amount = transaction.Amount,
            Description = transaction.Description,
            ExchangeRate = transaction.ExchangeRate,
            CategoryName = targetCategory.Name,
            CategoryIcon = targetCategory.Icon,
            MerchantName = targetMerchant?.Name,
            CountryName = targetCountry.Name,
            CurrencySymbol = targetCurrency.Symbol,
            AddedBy = _currentUserService.Username
        };
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

        var allMerchants = await _masterDataService.GetMerchantsAsync();
        var allCategories = await _masterDataService.GetCategoriesAsync(); // from cache

        // Merchant
        MerchantResponseDto? matchedMerchant = null;
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

        CategoryResponseDto? targetCategory = null;

        if (matchedMerchant != null)
        {
            if (matchedMerchant.DefaultCategory != null)
            {
                targetCategory = allCategories.FirstOrDefault(c => c.Id == matchedMerchant.DefaultCategory.Id);
            }

            processingText = processingText.Remove(earliestIndex, matchedLength);
        }
        else
        {
            var categoryRules = await _masterDataService.GetCategoryRulesAsync();

            var matchedRule = categoryRules
                .OrderByDescending(r => r.Keyword.Length)
                .FirstOrDefault(r => processingTextLower.Contains(r.Keyword.ToLower(_trCulture), StringComparison.Ordinal));

            if (matchedRule != null)
            {
                targetCategory = allCategories.FirstOrDefault(c => c.Id == matchedRule.CategoryId);

                int ruleIndex = processingTextLower.IndexOf(matchedRule.Keyword.ToLower(_trCulture), StringComparison.Ordinal);
                if(ruleIndex >= 0)
                {
                    var tempText = processingText.Remove(ruleIndex, matchedRule.Keyword.Length).Trim();
                    if (!string.IsNullOrWhiteSpace(tempText))
                    {
                        processingText = tempText;
                    }
                }
            }
        }

        if (targetCategory == null)
        {
            targetCategory = allCategories.FirstOrDefault(c => c.Name.Equals("DİĞER", StringComparison.OrdinalIgnoreCase));
        }
        if (targetCategory == null) throw new ArgumentException("There is no category named 'OTHER' in the system");

        // Clear the description
        processingText = Regex.Replace(processingText, @"\s+", " ").Trim();

        // Default settings
        var allCountries = await _masterDataService.GetCountriesAsync();
        var defaultCountry = allCountries.FirstOrDefault(c => c.Code.ToUpperInvariant() == "TR");

        var allCurrencies = await _masterDataService.GetCurrenciesAsync();
        var defaultCurrency = allCurrencies.FirstOrDefault(c => c.Code.ToUpperInvariant() == "TRY");

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
            CurrencyId = defaultCurrency?.Id,
            UserId = _currentUserService.UserId
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
            CurrencySymbol = defaultCurrency?.Symbol ?? string.Empty,
            AddedBy = _currentUserService.Username
        };
    }

    public async Task<TransactionResponse> UpdateTransactionAsync(Guid id, UpdateTransactionRequest request)
    {
        var transaction = await _context.Transactions.FindAsync(id);
        if (transaction is null) 
            throw new KeyNotFoundException($"Transaction not found. ID: {id}");

        var allCategories = await _masterDataService.GetCategoriesAsync();
        var allMerchants = await _masterDataService.GetMerchantsAsync();
        var allCountries = await _masterDataService.GetCountriesAsync();
        var allCurrencies = await _masterDataService.GetCurrenciesAsync();

        if (request.Amount.HasValue) transaction.Amount = request.Amount.Value;
        if (request.Date.HasValue) transaction.TransactionDate = request.Date.Value;
        if (request.ExchangeRate.HasValue) transaction.ExchangeRate = request.ExchangeRate.Value;
        if (request.Description != null) transaction.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description;

        if (request.CategoryId.HasValue && request.CategoryId.Value != Guid.Empty)
        {
            if (!allCategories.Any(c => c.Id == request.CategoryId.Value))
                throw new ArgumentException($"Invalid Category ID: {request.CategoryId.Value}");

            transaction.CategoryId = request.CategoryId.Value;
        }

        if (request.MerchantId.HasValue)
        {
            if (request.MerchantId.Value == Guid.Empty)
            {
                transaction.MerchantId = null;
            }
            else
            {
                if (!allMerchants.Any(m => m.Id == request.MerchantId.Value))
                    throw new ArgumentException($"Invalid Merchant ID: {request.MerchantId.Value}");

                transaction.MerchantId = request.MerchantId.Value;
            }
        }

        if (request.CountryId.HasValue && request.CountryId.Value != Guid.Empty)
        {
            if (!allCountries.Any(c => c.Id == request.CountryId.Value))
                throw new ArgumentException($"Invalid Country ID: {request.CountryId.Value}");

            transaction.CountryId = request.CountryId.Value;
        }

        if (request.CurrencyId.HasValue && request.CurrencyId.Value != Guid.Empty)
        {
            if (!allCurrencies.Any(c => c.Id == request.CurrencyId.Value))
                throw new ArgumentException($"Invalid Currency ID: {request.CurrencyId.Value}");

            transaction.CurrencyId = request.CurrencyId.Value;
        }

        await _context.SaveChangesAsync();

        var finalCategory = allCategories.FirstOrDefault(c => c.Id == transaction.CategoryId);
        var finalMerchant = transaction.MerchantId.HasValue ? allMerchants.FirstOrDefault(m => m.Id == transaction.MerchantId.Value) : null;
        var finalCountry = allCountries.FirstOrDefault(c => c.Id == transaction.CountryId);
        var finalCurrency = allCurrencies.FirstOrDefault(c => c.Id == transaction.CurrencyId);

        return new TransactionResponse
        {
            Id = transaction.Id,
            Date = transaction.TransactionDate,
            Amount = transaction.Amount,
            Description = transaction.Description,
            ExchangeRate = transaction.ExchangeRate,
            CategoryName = finalCategory?.Name ?? string.Empty,
            CategoryIcon = finalCategory?.Icon,
            MerchantName = finalMerchant?.Name,
            CountryName = finalCountry?.Name ?? string.Empty,
            CurrencySymbol = finalCurrency?.Symbol ?? string.Empty,
            AddedBy = string.Empty
        };
    }

    public async Task DeleteTransactionAsync(Guid id)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == _currentUserService.UserId);
        if (transaction is null) 
            throw new KeyNotFoundException($"Transaction not found or does not belong to your user. Trx ID: {id}");

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();
    }

    public async Task<int> CreateBulkTransactionsAsync(List<CreateTransactionRequest> requests)
    {
        if (requests == null || !requests.Any()) return 0;

        var allCategories = await _masterDataService.GetCategoriesAsync();
        var allMerchants = await _masterDataService.GetMerchantsAsync();
        var allCountries = await _masterDataService.GetCountriesAsync();
        var allCurrencies = await _masterDataService.GetCurrenciesAsync();

        var defaultCategory = allCategories.FirstOrDefault(c => c.Name.Equals("DİĞER", StringComparison.OrdinalIgnoreCase));
        var defaultCountry = allCountries.FirstOrDefault(c => c.Code.Equals("TR", StringComparison.OrdinalIgnoreCase));
        var defaultCurrency = allCurrencies.FirstOrDefault(c => c.Code.Equals("TRY", StringComparison.OrdinalIgnoreCase));

        if (defaultCategory == null || defaultCountry == null || defaultCurrency == null)
        {
            throw new InvalidOperationException("Core default master data (OTHER, TR, TRY) is missing from the database.");
        }

        var transactionsToInsert = new List<Transaction>();
        var exchangeRateTasks = new List<Task>();

        foreach(var req in requests)
        {
            var targetCategoryId = req.CategoryId.HasValue && req.CategoryId != Guid.Empty
                ? req.CategoryId.Value
                : defaultCategory.Id;

            Guid? targetMerchantId = req.MerchantId.HasValue && req.MerchantId != Guid.Empty
                ? req.MerchantId.Value
                : null;
            
            var targetCountryId = req.CountryId.HasValue && req.CountryId != Guid.Empty 
                ? req.CountryId.Value 
                : defaultCountry.Id;

            var targetCurrencyId = req.CurrencyId.HasValue && req.CurrencyId != Guid.Empty 
                ? req.CurrencyId.Value 
                : defaultCurrency.Id;

            var targetCurrencyCode = allCurrencies.FirstOrDefault(c => c.Id == targetCurrencyId)?.Code ?? "TRY";
            var transactionDate = req.Date ?? DateTime.UtcNow;

            var transaction = new Transaction
            {
                Id = Guid.NewGuid(),
                TransactionDate = transactionDate,
                Amount = req.Amount,
                Description = req.Description,
                CategoryId = targetCategoryId,
                MerchantId = targetMerchantId,
                CountryId = targetCountryId,
                CurrencyId = targetCurrencyId,
                UserId = _currentUserService.UserId
            };

            if(targetCurrencyCode.Equals("TRY", StringComparison.OrdinalIgnoreCase))
            {
                transaction.ExchangeRate = 1.0m; // skip gRPC call if the currency is already the base currency (TRY)
            }
            else
            {
                // resolve exchange rates asynchronously
                var rateTask = _exchangeRateService.GetExchangeRateAsync(targetCurrencyCode, transactionDate)
                    .ContinueWith(t => transaction.ExchangeRate = t.Result);
                
                exchangeRateTasks.Add(rateTask);
            }

            transactionsToInsert.Add(transaction);
        }

        if (exchangeRateTasks.Any()) // await all parallel gRPC calls simultaneously (if any exist)
        {
            await Task.WhenAll(exchangeRateTasks);
        }

        await _context.Transactions.AddRangeAsync(transactionsToInsert);
        await _context.SaveChangesAsync();

        return transactionsToInsert.Count;
    }

    public async Task<string> ParseStatementAsync(IFormFile file)
    {
        // 1. Dosya Validasyonları
        if (file == null || file.Length == 0)
            throw new ArgumentException("No file was uploaded.");

        if (file.Length > 5 * 1024 * 1024)
            throw new ArgumentException("File size exceeds the strict 5MB limit.");

        if (!file.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Invalid file type. Only native PDF documents are permitted.");

        var nlpServiceUrl = _configuration["NLP_SERVICE_URL"]; 
        var nlpApiSecret = _configuration["NLP_API_SECRET"];

        if (string.IsNullOrEmpty(nlpServiceUrl) || string.IsNullOrEmpty(nlpApiSecret))
            throw new InvalidOperationException("Internal NLP service routing configurations are missing.");

        // from cache
        var allCategories = await _masterDataService.GetCategoriesAsync();
        var allMerchants = await _masterDataService.GetMerchantsAsync();

        var categoriesJson = JsonSerializer.Serialize(allCategories.Where(c => c.ParentCategory != null).Select(c => c.Name));
        var merchantsJson = JsonSerializer.Serialize(allMerchants.Select(m => new {
            name = m.Name,
            defaultCategoryName = m.DefaultCategory?.Name
        }));

        // python request prep.
        var client = _httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Add("X-API-Key", nlpApiSecret);

        using var content = new MultipartFormDataContent();

        using var fileStream = file.OpenReadStream();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(fileContent, "file", file.FileName);
        
        content.Add(new StringContent(categoriesJson), "categories");
        content.Add(new StringContent(merchantsJson), "merchants");

        var response = await client.PostAsync(nlpServiceUrl, content);
        var responseBody = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"NLP Microservice execution failed. Details: {responseBody}");

        return responseBody;
    }
}