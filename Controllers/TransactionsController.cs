using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using WalletApp.Data;
using WalletApp.Entities;
using WalletApp.Dtos;
using WalletApp.Services;

namespace WalletApp.Controllers;

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
    public async Task<ActionResult<IEnumerable<TransactionResponse>>> GetTransactions()
    {
        var transactions = await _transactionService.GetTransactionsAsync();
        return Ok(transactions);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<TransactionResponse>> GetTransaction(Guid id)
    {
        var transaction = await _transactionService.GetTransactionByIdAsync(id);
        if(transaction is null) return NotFound();

        return Ok(transaction);
    }

    [HttpPost]
    public async Task<ActionResult<Transaction>> PostTransaction(CreateTransactionRequest request)
    {
        try
        {
            var transaction = await _transactionService.CreateTransactionAsync(request);
            return CreatedAtAction(nameof(GetTransaction), new { id = transaction.Id }, transaction);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        var isDeleted = await _transactionService.DeleteTransactionAsync(id);
        if(!isDeleted) return NotFound();
        
        return NoContent();
    }

    [HttpPost("quick-add")]
    public async Task<ActionResult<TransactionResponse>> QuickAddTransaction([FromBody] QuickAddRequest request)
    {
        try
        {
            var response = await _transactionService.QuickAddTransactionAsync(request);
            return CreatedAtAction(nameof(GetTransaction), new { id = response.Id }, response);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}