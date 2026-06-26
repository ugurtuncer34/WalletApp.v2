namespace WalletApp.Services;

public interface ICurrentUserService
{
    Guid UserId { get; }
    string Username { get; }
}