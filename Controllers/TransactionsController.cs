using Microsoft.AspNetCore.Mvc;
using WalletApp.Entities;
using WalletApp.Dtos;
using WalletApp.Services;
using Microsoft.AspNetCore.Authorization;
using WalletApp.Filters;

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
    [Idempotency]
    public async Task<ActionResult<TransactionResponse>> PostTransaction(CreateTransactionRequest request)
    {

        var response = await _transactionService.CreateTransactionAsync(request);
        return CreatedAtAction(nameof(GetTransaction), new { id = response.Id }, response);

    }

    [HttpPost("quick-add")]
    [Idempotency]
    public async Task<ActionResult<TransactionResponse>> QuickAddTransaction(QuickAddRequest request)
    {

        var response = await _transactionService.QuickAddTransactionAsync(request);
        return CreatedAtAction(nameof(GetTransaction), new { id = response.Id }, response);

    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TransactionResponse>> PutTransaction(Guid id, UpdateTransactionRequest request)
    {
        var updated = await _transactionService.UpdateTransactionAsync(id, request);
        return Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        await _transactionService.DeleteTransactionAsync(id);
        return NoContent();
    }

    [HttpPost("bulk")]
    [Idempotency]
    public async Task<IActionResult> PostBulkTransactions([FromBody] List<CreateTransactionRequest> requests)
    {
        if (requests == null || !requests.Any())
        {
            return BadRequest(new { Message = "Transaction list cannot be empty." });
        }

        var insertedCount = await _transactionService.CreateBulkTransactionsAsync(requests);

        return Ok( new
        {
            Success = true,
            Message = $"{insertedCount} transactions successfully processed.",
            Count = insertedCount
        });
    }

    [HttpPost("parse-statement")]
    [Idempotency]
    public async Task<IActionResult> ParseStatementProxy(IFormFile file)
    {
        var result = await _transactionService.ParseStatementAsync(file);
        return Content(result, "application/json");
    }
}