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
    private readonly IRecurringTransactionService _recurringService;

    public RecurringTransactionsController(IRecurringTransactionService recurringService)
    {
        _recurringService = recurringService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMySubscriptions()
    {
        var subscriptions = await _recurringService.GetMySubscriptionsAsync();
        return Ok(subscriptions);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSubscription(CreateRecurringRequest request)
    {
        var id = await _recurringService.CreateSubscriptionAsync(request);
        return Ok(new { Message = "Subscription / Installment created successfully.", Id = id });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSubscription(Guid id, UpdateRecurringRequest request)
    {
        await _recurringService.UpdateSubscriptionAsync(id, request);
        return Ok(new { Message = "Subscription updated successfully." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelSubscription(Guid id)
    {
        await _recurringService.CancelSubscriptionAsync(id);
        return Ok(new { Message = "Subscription successfully canceled." });
    }
}