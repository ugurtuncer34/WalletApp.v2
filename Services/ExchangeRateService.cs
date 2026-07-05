using System.Text.Json;
using WalletApp.Protos;

namespace WalletApp.Services;

public interface IExchangeRateService
{
    Task<decimal> GetExchangeRateAsync(string currencyCode, DateTime date);
}

public class ExchangeRateService : IExchangeRateService
{
    // private readonly HttpClient _httpClient;
    private readonly Protos.ExchangeRateService.ExchangeRateServiceClient _grpcClient;

    // public ExchangeRateService(HttpClient httpClient)
    // {
    //     _httpClient = httpClient;
    // }
    public ExchangeRateService(Protos.ExchangeRateService.ExchangeRateServiceClient grpcClient)
    {
        _grpcClient = grpcClient;
    }

    public async Task<decimal> GetExchangeRateAsync(string currencyCode, DateTime date)
    {
        if (currencyCode.Equals("TRY", StringComparison.OrdinalIgnoreCase))
            return 1m;
        
        var dateStr = date.ToString("yyyy-MM-dd"); // Go service format
        var request = new RateRequest{ Currency = currencyCode, Date = dateStr };
        var response = await _grpcClient.GetExchangeRateAsync(request);
        return Convert.ToDecimal(response.Rate);
        
        // var response = await _httpClient.GetAsync($"api/rates?currency={currencyCode}&date={dateStr}");

        // if (!response.IsSuccessStatusCode)
        // {
        //     throw new Exception($"Failed to retrieve Exchange Rate. Error from Go service: {response.StatusCode}");
        // }

        // var content = await response.Content.ReadAsStringAsync();

        // var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        // var result = JsonSerializer.Deserialize<ExchangeRateResponse>(content, options);

        // return result?.Rate ?? 1m;
    }
}

// Dto for returning JSON from Go
public class ExchangeRateResponse
{
    public string Currency { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public decimal Rate { get; set; }
    public string Source { get; set; } = string.Empty;
}