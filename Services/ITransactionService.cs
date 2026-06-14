using WalletApp.Dtos;
using WalletApp.Entities;

namespace WalletApp.Services;

public interface ITransactionService
{
    Task<PagedResult<TransactionResponse>> GetTransactionsAsync(TransactionQueryParameters queryParams);
    Task<TransactionResponse> GetTransactionByIdAsync(Guid id);
    Task<Transaction> CreateTransactionAsync(CreateTransactionRequest request);
    Task<TransactionResponse> QuickAddTransactionAsync(QuickAddRequest request);
    Task DeleteTransactionAsync(Guid id);
}