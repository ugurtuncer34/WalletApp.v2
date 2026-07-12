using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using WalletApp.Data;
using WalletApp.Entities;

namespace WalletApp.Tests;

public class ConcurrencyIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public ConcurrencyIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Optimistic_Lock_Should_Prevent_Concurrent_Transaction_Updates()
    {
        // Arrange: First add a Category, and then a Transaction linked to it.
        using var setupScope = _factory.Services.CreateScope();
        var setupDb = setupScope.ServiceProvider.GetRequiredService<AppDbContext>();

        // Create a dummy User to satisfy the Foreign Key constraint
        var userId = Guid.NewGuid();
        setupDb.Users.Add(new User 
        { 
            Id = userId, Username = "testuser", PasswordHash = "hash", Role = "User", CreatedAt = DateTime.UtcNow 
        });

        // Create a dummy Category
        var categoryId = Guid.NewGuid();
        setupDb.Categories.Add(new Category { Id = categoryId, Name = "Market", Icon = "🛒", CreatedAt = DateTime.UtcNow });
        
        // Create the Transaction linked to the User and Category
        var transactionId = Guid.NewGuid();
        var transaction = new Transaction 
        { 
            Id = transactionId, 
            CategoryId = categoryId, 
            Amount = 100m, 
            Description = "Test Expense",
            CreatedAt = DateTime.UtcNow, 
            UserId = userId // Matches the newly created User's ID
        };
        
        setupDb.Transactions.Add(transaction);
        await setupDb.SaveChangesAsync();

        // Act: 2 different Users (Contexts) fetch the exact same transaction at the same millisecond.
        using var scope1 = _factory.Services.CreateScope();
        var user1Db = scope1.ServiceProvider.GetRequiredService<AppDbContext>();
        var user1Transaction = await user1Db.Transactions.FindAsync(transactionId);

        using var scope2 = _factory.Services.CreateScope();
        var user2Db = scope2.ServiceProvider.GetRequiredService<AppDbContext>();
        var user2Transaction = await user2Db.Transactions.FindAsync(transactionId);

        // Both users update the transaction amount simultaneously
        user1Transaction!.Amount = 150; // User 1 update
        user2Transaction!.Amount = 500; // User 2 update at the same time

        // Assert: User 1 acts fast and saves first (This operation will succeed)
        await user1Db.SaveChangesAsync();

        // When User 2 tries to save, the system should detect the conflict and lock the DB 
        // because the 'xmin' (RowVersion) in the Transaction table was changed by User 1!
        Func<Task> concurrentSaveAction = async () => await user2Db.SaveChangesAsync();

        // EF Core will throw the expected Concurrency exception, protecting data integrity
        await concurrentSaveAction.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }
}