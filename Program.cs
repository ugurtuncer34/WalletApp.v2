using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Serilog;
using Hangfire;
using Hangfire.PostgreSql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using WalletApp.Data;
using WalletApp.Middleware;
using WalletApp.Services;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Serilog
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    // .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    // .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File(
        new Serilog.Formatting.Compact.CompactJsonFormatter(),
        "Logs/walletapp-log-.json",
        rollingInterval: RollingInterval.Day
    )
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        var secretKey = builder.Configuration["JwtSettings:SecretKey"];

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"],
            ValidAudience = builder.Configuration["JwtSettings:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey!))
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var services = context.HttpContext.RequestServices;

                var cache = services.GetRequiredService<IDistributedCache>();

                // 1. Check whether this exact token was revoked
                var jti = context.Principal?
                    .FindFirst(JwtRegisteredClaimNames.Jti)?
                    .Value;

                if (!string.IsNullOrEmpty(jti))
                {
                    var isBlacklisted = await cache.GetStringAsync($"blacklist_{jti}");

                    if (!string.IsNullOrEmpty(isBlacklisted))
                    {
                        context.Fail("Login required.");
                        return;
                    }
                }

                // 2. Check whether the user still exists and is active
                var userIdValue =
                    context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? context.Principal?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

                if (!Guid.TryParse(userIdValue, out var userId))
                {
                    context.Fail("Invalid user identity.");
                    return;
                }

                var dbContext = services.GetRequiredService<AppDbContext>();

                var isActiveUser = await dbContext.Users
                    .AsNoTracking()
                    .AnyAsync(user => user.Id == userId && user.IsActive);

                if (!isActiveUser)
                {
                    context.Fail("Login required.");
                }
            }
        };
    });

// HANGFIRE Services
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(c =>
        c.UseNpgsqlConnection(builder.Configuration.GetConnectionString("DefaultConnection"))));

// Start Hangfire server
builder.Services.AddHangfireServer();

// OPENTELEMETRY Tracing
builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            // service name to appear on Grafana/Jaeger
            .SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("FamilyFinance.Backend"))

            // tracing places
            .AddAspNetCoreInstrumentation() // HTTP requests
            .AddHttpClientInstrumentation() // Python HTTP requests
            .AddGrpcClientInstrumentation() // Go gRPC calls
            .AddEntityFrameworkCoreInstrumentation()

            // place to send data (future Jaeger address)
            .AddOtlpExporter(opts =>
            {
                // Coolify OTLP_ENDPOINT, if not present, use default Jaeger port
                opts.Endpoint = new Uri(builder.Configuration["OTLP_ENDPOINT"] ?? "http://localhost:4317");
            });
    });

builder.Services.AddControllers()
    .AddJsonOptions(x => x.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles);

builder.Services.AddDistributedMemoryCache();
builder.Services.AddHttpClient(); // for secure backend-to-backend proxy communication
builder.Services.AddScoped<IMasterDataService, MasterDataService>();
builder.Services.AddScoped<ITransactionService, TransactionService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ISubscriptionJobService, SubscriptionJobService>();
builder.Services.AddScoped<IRecurringTransactionService, RecurringTransactionService>();
builder.Services.AddGrpcClient<WalletApp.Protos.ExchangeRateService.ExchangeRateServiceClient>(options =>
{
    var grpcUrl = builder.Configuration["GoGrpcServiceUrl"] ?? "http://localhost:50051";
    options.Address = new Uri(grpcUrl);
});
builder.Services.AddScoped<IExchangeRateService, ExchangeRateService>();
builder.Services.AddScoped<ICryptoService, CryptoService>();

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "WalletApp", Version = "v1" });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header. Ex: 'Bearer {token}'",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    c.AddSecurityRequirement((document) => new()
    {
        [new("Bearer", document)] = []
    });
});

// Cors
var corsOrigins = builder.Configuration["AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
    ?? ["http://localhost:3000", "http://localhost:5173"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();

app.UseAuthorization();

// Public for now, Admin role control will be added
app.UseHangfireDashboard();
// Hangfire timing
RecurringJob.AddOrUpdate<ISubscriptionJobService>(
    "Subscription_Expense_Task", // Task name for panel
    job => job.ProcessRecurringTransactionAsync(), // Which task should run
    Cron.Daily(1) // Every night UTC 1am
);

app.MapControllers();

// AUTO MIGRATION
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        context.Database.Migrate();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}

app.Run();

public partial class Program { }
