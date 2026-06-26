namespace WalletApp.Services;

public interface ICurrentUserService
{
    Guid UserId { get; }
}