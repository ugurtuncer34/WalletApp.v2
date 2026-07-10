using WalletApp.Dtos;

namespace WalletApp.Services;

public interface ITransactionService
{
    Task<PagedResult<TransactionResponse>> GetTransactionsAsync(TransactionQueryParameters queryParams);
    Task<TransactionResponse> GetTransactionByIdAsync(Guid id);
    Task<TransactionResponse> CreateTransactionAsync(CreateTransactionRequest request);
    Task<TransactionResponse> QuickAddTransactionAsync(QuickAddRequest request);
    Task<TransactionResponse> UpdateTransactionAsync(Guid id, UpdateTransactionRequest request);
    Task DeleteTransactionAsync(Guid id);
    Task<int> CreateBulkTransactionsAsync(List<CreateTransactionRequest> requests);
    Task<string> ParseStatementAsync(IFormFile file);
}