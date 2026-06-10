using WalletApp.Dtos;
using WalletApp.Entities;

namespace WalletApp.Services;

public interface ITransactionService
{
    Task<IEnumerable<TransactionResponse>> GetTransactionsAsync();
    Task<TransactionResponse?> GetTransactionByIdAsync(Guid id);
    Task<Transaction> CreateTransactionAsync(CreateTransactionRequest request);
    Task<TransactionResponse> QuickAddTransactionAsync(QuickAddRequest request);
    Task<bool> DeleteTransactionAsync(Guid id);
}