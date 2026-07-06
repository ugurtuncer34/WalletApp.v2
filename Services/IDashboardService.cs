using WalletApp.Dtos;

namespace WalletApp.Services;

public interface IDashboardService
{
    Task<DashboardResponse> GetMonthlyDashboardAsync(int year, int month, Guid? userId = null, Guid? currencyId = null);
}