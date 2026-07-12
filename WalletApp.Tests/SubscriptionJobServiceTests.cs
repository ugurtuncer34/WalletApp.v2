using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using WalletApp.Data;
using WalletApp.Entities;
using WalletApp.Enums;
using WalletApp.Services;

namespace WalletApp.Tests;

public class SubscriptionJobServiceTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SubscriptionJobServiceTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Should_Process_Due_Subscription_And_Advance_NextExecutionDate()
    {
        // Arrange: Create a User, a Category, and a due Subscription
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Resolve the IDistributedCache from the test DI container
        var cache = scope.ServiceProvider.GetRequiredService<IDistributedCache>();

        // Dummy User
        var userId = Guid.NewGuid();
        db.Users.Add(new User
        {
            Id = userId,
            Username = "testuser",
            PasswordHash = "hash",
            Role = "User",
            CreatedAt = DateTime.UtcNow
        });

        // Dummy Category
        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            Name = "Digital Services",
            Icon = "🌐",
            CreatedAt = DateTime.UtcNow
        });

        // Seed default Country and Currency since SubscriptionJobService queries them
        db.Countries.Add(new Country { Id = Guid.NewGuid(), Name = "Türkiye", Code = "TR", CreatedAt = DateTime.UtcNow });
        db.Currencies.Add(new Currency { Id = Guid.NewGuid(), Name = "Turkish Lira", Code = "TRY", Symbol = "₺", CreatedAt = DateTime.UtcNow });

        // Create a Recurring Transaction that was due YESTERDAY
        var recurringId = Guid.NewGuid();
        var recurring = new RecurringTransaction
        {
            Id = recurringId,
            UserId = userId,
            CategoryId = categoryId,
            Name = "Netflix Subscription",
            Amount = 250m,
            Frequency = RecurringFrequency.Monthly, // Using the correct Enum
            IsActive = true,
            IsInstallment = false,
            NextExecutionDate = DateTime.UtcNow.AddDays(-1), // Due in the past
            CreatedAt = DateTime.UtcNow
        };

        db.RecurringTransactions.Add(recurring);
        await db.SaveChangesAsync();

        // Instantiate the Hangfire background job service
        var jobService = new SubscriptionJobService(db, cache);

        // Act: Manually trigger the night job
        await jobService.ProcessRecurringTransactionAsync();

        // Assert: Verify the database changes

        // A real Transaction should have been created in the ledger
        var createdTransaction = await db.Transactions
            .FirstOrDefaultAsync(t => t.CategoryId == categoryId && t.Amount == 250m);

        createdTransaction.Should().NotBeNull();
        createdTransaction!.Description.Should().Contain("Netflix");

        // The subscription's next execution date should have been pushed forward (~1 month)
        var updatedRecurring = await db.RecurringTransactions.FindAsync(recurringId);

        updatedRecurring.Should().NotBeNull();
        updatedRecurring!.NextExecutionDate.Should().BeAfter(DateTime.UtcNow);
    }
}