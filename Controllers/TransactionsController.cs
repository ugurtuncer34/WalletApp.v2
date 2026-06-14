using Microsoft.AspNetCore.Mvc;
using WalletApp.Entities;
using WalletApp.Dtos;
using WalletApp.Services;
using Microsoft.AspNetCore.Authorization;

namespace WalletApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _transactionService;
    public TransactionsController(ITransactionService transactionService)
    {
        _transactionService = transactionService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetTransactions([FromQuery] TransactionQueryParameters queryParams)
    {
        var transactions = await _transactionService.GetTransactionsAsync(queryParams);
        return Ok(transactions);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionResponse>> GetTransaction(Guid id)
    {
        var transaction = await _transactionService.GetTransactionByIdAsync(id);
        return Ok(transaction);
    }

    [HttpPost]
    public async Task<ActionResult<Transaction>> PostTransaction(CreateTransactionRequest request)
    {

        var transaction = await _transactionService.CreateTransactionAsync(request);
        return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);

    }

    [HttpPost("quick-add")]
    public async Task<ActionResult<TransactionResponse>> QuickAddTransaction(QuickAddRequest request)
    {

        var response = await _transactionService.QuickAddTransactionAsync(request);
        return CreatedAtAction(nameof(GetTransaction), new { id = response.Id }, response);

    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        await _transactionService.DeleteTransactionAsync(id);
        return NoContent();
    }
}