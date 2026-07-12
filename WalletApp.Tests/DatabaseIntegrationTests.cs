using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WalletApp.Data;
using WalletApp.Entities;

namespace WalletApp.Tests;

public class DatabaseIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public DatabaseIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Should_Save_Category_To_Testcontainer_Database()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var newCategory = new Category
        {
            Id = Guid.NewGuid(),
            Name = "Test Market",
            Icon = "🛒"
        };

        // Act
        dbContext.Categories.Add(newCategory);
        await dbContext.SaveChangesAsync();

        // Assert
        var savedCategory = await dbContext.Categories.FindAsync(newCategory.Id);

        savedCategory.Should().NotBeNull();
        savedCategory!.Name.Should().Be("Test Market");
        savedCategory.Icon.Should().Be("🛒");
    }
}