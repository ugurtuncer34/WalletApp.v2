using System.Net.Http.Headers;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using WalletApp.Data;
using WalletApp.Dtos;
using WalletApp.Entities;

namespace WalletApp.Tests;

public class UserJourneyE2ETests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UserJourneyE2ETests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        // Create a client to send requests to our API hosted on Testcontainers
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Full_User_Journey_Should_Work_From_Registration_To_Dashboard()
    {
        // Arrange: Since the database is empty, seed the default "DİĞER" category 
        // and TR/TRY definitions to prevent the Quick Add method from failing.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Categories.Add(new Category { Id = Guid.NewGuid(), Name = "DİĞER", Icon = "🌀" });
            db.Countries.Add(new Country { Id = Guid.NewGuid(), Name = "Türkiye", Code = "TR" });
            db.Currencies.Add(new Currency { Id = Guid.NewGuid(), Name = "Turkish Lira", Code = "TRY", Symbol = "₺" });
            await db.SaveChangesAsync();
        }

        // Register & Login
        var registerReq = new UserLoginRequest { Username = "e2e_user", Password = "e2e_password!" };
        var registerRes = await _client.PostAsJsonAsync("/api/Auth/register", registerReq);
        registerRes.IsSuccessStatusCode.Should().BeTrue("User registration should be successful.");

        var authData = await registerRes.Content.ReadFromJsonAsync<AuthResponse>();
        authData.Should().NotBeNull();
        authData!.Token.Should().NotBeNullOrEmpty();

        // Attach the authorization (JWT) and Idempotency keys to the headers for all subsequent requests
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", authData.Token);
        _client.DefaultRequestHeaders.Add("X-Idempotency-Key", Guid.NewGuid().ToString());

        // Add Transaction (Quick Add)
        var quickAddReq = new QuickAddRequest { Text = "500 Dışarıda Yemek" };
        var quickAddRes = await _client.PostAsJsonAsync("/api/Transactions/quick-add", quickAddReq);
        quickAddRes.IsSuccessStatusCode.Should().BeTrue("Quick add transaction should be successful.");

        var trxData = await quickAddRes.Content.ReadFromJsonAsync<TransactionResponse>();
        trxData.Should().NotBeNull();
        trxData!.Amount.Should().Be(500m);

        // Check Dashboard (Did the expense reflect?)
        var now = DateTime.UtcNow;
        var dashboardRes = await _client.GetAsync($"/api/Dashboard?year={now.Year}&month={now.Month}");
        dashboardRes.IsSuccessStatusCode.Should().BeTrue("Dashboard data should be fetched successfully.");

        var dashboardData = await dashboardRes.Content.ReadFromJsonAsync<DashboardResponse>();
        dashboardData.Should().NotBeNull();
        
        // The core of the wallet: The 500 amount we added must be reflected on the Dashboard!
        dashboardData!.TotalMonthlyExpense.Should().Be(500m);
    }
}