using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WalletApp.Data;
using WalletApp.Dtos;
using WalletApp.Entities;
using WalletApp.Services;

namespace WalletApp.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class RecurringTransactionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public RecurringTransactionsController(AppDbContext context, ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMySubscriptions()
    {
        var subscriptions = await _context.RecurringTransactions
            .Include(r => r.Category)
            .Include(r => r.Merchant)
            .Where(r => r.UserId == _currentUserService.UserId && r.IsActive)
            .OrderBy(r => r.NextExecutionDate)
            .Select(r => new
            {
                r.Id,
                r.Name,
                r.Amount,
                r.Frequency,
                r.NextExecutionDate,
                r.IsInstallment,
                r.TotalInstallments,
                r.ProcessedInstallments,
                CategoryName = r.Category.Name,
                MerchantName = r.Merchant != null ? r.Merchant.Name : null
            })
            .ToListAsync();
        
        return Ok(subscriptions);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubscription(CreateRecurringRequest request)
    {
        var recurringTransaction = new RecurringTransaction
        {
            Id = Guid.NewGuid(),
            UserId = _currentUserService.UserId,
            Name = request.Name,
            Description = request.Description,
            Amount = request.Amount,
            CategoryId = request.CategoryId,
            MerchantId = request.MerchantId,
            Frequency = request.Frequency,
            NextExecutionDate = request.StartDate, // first shot date for trigger
            IsActive = true,
            IsInstallment = request.IsInstallment,
            TotalInstallments = request.IsInstallment ? request.TotalInstallments : null,
            ProcessedInstallments = 0
        };

        _context.RecurringTransactions.Add(recurringTransaction);
        await _context.SaveChangesAsync();

        return Ok(new { Message = "Subscription / Installment created successfully.", Id = recurringTransaction.Id });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelSubscription(Guid id)
    {
        var subscription = await _context.RecurringTransactions
            .FirstOrDefaultAsync(r => r.Id == id && r.UserId == _currentUserService.UserId);
        
        if(subscription is null)
            return NotFound("Could not find subscription or does not belong to you.");

        subscription.IsActive = false;

        await _context.SaveChangesAsync();

        return Ok(new { Message = "Subscription successfully canceled." });
    }
}