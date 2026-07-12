using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using WalletApp.Data;

namespace WalletApp.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Postgresql container to run on Docker
    private readonly PostgreSqlContainer _dbContainer = new PostgreSqlBuilder("postgres:alpine")
        .WithDatabase("walletapp_testdb")
        .WithUsername("postgres")
        .WithPassword("testpassword")
        .WithCleanUp(true) // remove container after test completes
        .Build();

    // start container before tests run
    public async Task InitializeAsync()
    {
        await _dbContainer.StartAsync();
    }

    // when app is starting, overwrite settings
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Enable registration for E2E tests
        builder.ConfigureAppConfiguration((context, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AllowRegistration", "true" },
                { "ConnectionStrings:DefaultConnection", _dbContainer.GetConnectionString() }, // hangfire
                { "Jwt:Secret", "ThisIsASuperLongAndSecureDummySecretKeyForTestingYourJWTSigning123!" }
            });
        });

        builder.ConfigureServices(services =>
        {
            // delete original db from services
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));

            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // connect db in testcontainer on docker
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(_dbContainer.GetConnectionString());
            });

            // auto create db tables
            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.Migrate();
        });
    }

    // Stop and dispose of the application and Docker container AFTER the tests finish
    public new async Task DisposeAsync()
    {
        // Gracefully shut down the ASP.NET Core host and Hangfire background server FIRST
        await base.DisposeAsync();
        // Safely destroy the PostgreSQL container AFTER all connections are closed
        await _dbContainer.DisposeAsync();
    }
}