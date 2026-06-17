using WalletApp.Dtos;

namespace WalletApp.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(UserLoginRequest request);
    Task<AuthResponse> LoginAsync(UserLoginRequest request);
    Task ChangePasswordAsync(Guid id, ChangePasswordRequest request);
}