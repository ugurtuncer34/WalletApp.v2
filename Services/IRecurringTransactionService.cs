using WalletApp.Dtos;

namespace WalletApp.Services;

public interface IRecurringTransactionService
{
    Task<List<RecurringTransactionResponse>> GetMySubscriptionsAsync();
    Task<Guid> CreateSubscriptionAsync(CreateRecurringRequest request);
    Task UpdateSubscriptionAsync(Guid id, UpdateRecurringRequest request);
    Task CancelSubscriptionAsync(Guid id);
}