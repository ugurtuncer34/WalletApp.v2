using Grpc.Core; // for Metadata (gRPC use Metadada instead of Http Headers)
using WalletApp.Protos;

namespace WalletApp.Services;

public interface IExchangeRateService
{
    Task<decimal> GetExchangeRateAsync(string currencyCode, DateTime date);
    Task<decimal> GetCryptoRateAsync(string coinCode);
}

public class ExchangeRateService : IExchangeRateService
{
    private readonly Protos.ExchangeRateService.ExchangeRateServiceClient _grpcClient;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ExchangeRateService(
        Protos.ExchangeRateService.ExchangeRateServiceClient grpcClient,
        IHttpContextAccessor httpContextAccessor
    )
    {
        _grpcClient = grpcClient;
        _httpContextAccessor = httpContextAccessor;
    }

    private Metadata GetGrpcHeaders()
    {
        var correlationId = _httpContextAccessor.HttpContext?.Request.Headers["X-Correlation-ID"].FirstOrDefault()
            ?? Guid.NewGuid().ToString();
        
        return new Metadata {{ "x-correlation-id", correlationId }}; // gRPC metadata keys have to be lowercase
    }

    public async Task<decimal> GetExchangeRateAsync(string currencyCode, DateTime date)
    {
        if (currencyCode.Equals("TRY", StringComparison.OrdinalIgnoreCase))
            return 1m;

        var dateStr = date.ToString("yyyy-MM-dd"); // Go service format
        var request = new RateRequest { Currency = currencyCode, Date = dateStr };
        var response = await _grpcClient.GetExchangeRateAsync(request, GetGrpcHeaders());
        return Convert.ToDecimal(response.Rate);
    }

    public async Task<decimal> GetCryptoRateAsync(string coinCode)
    {
        var symbol = $"{coinCode.ToUpper()}USDT";
        var request = new CryptoRequest { Symbol = symbol };
        var response = await _grpcClient.GetCryptoRateAsync(request, GetGrpcHeaders());
        return Convert.ToDecimal(response.Price);
    }
}