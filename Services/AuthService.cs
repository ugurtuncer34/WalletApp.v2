using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;
using WalletApp.Data;
using WalletApp.Dtos;
using WalletApp.Entities;

namespace WalletApp.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IDistributedCache _cache;

    public AuthService(AppDbContext context, IConfiguration configuration, IDistributedCache cache)
    {
        _context = context;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<AuthResponse> RegisterAsync(UserLoginRequest request)
    {
        var allowRegistration = _configuration.GetValue<bool>("AllowRegistration", false); // default false
        if(!allowRegistration)
            throw new UnauthorizedAccessException("System is closed for new registrations.");
        
        var userExists = await _context.Users.AnyAsync(u => u.Username.ToLower() == request.Username.ToLower());
        if(userExists) throw new ArgumentException("Username already taken.");

        string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = request.Username,
            PasswordHash = passwordHash,
            Role = "User" // default role
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        string token = GenerateJwtToken(user);

        return new AuthResponse
        {
            Token = token,
            Username = user.Username
        };
    }

    public async Task<AuthResponse> LoginAsync(UserLoginRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == request.Username.ToLower());
        if(user is null) throw new ArgumentException("Wrong Username or Password.");

        bool isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if(!isPasswordValid) throw new ArgumentException("Wrong Username or Password.");

        string token = GenerateJwtToken(user);

        return new AuthResponse
        {
            Token = token,
            Username = user.Username
        };
    }

    public async Task<IEnumerable<UserResponse>> GetAllUsersAsync()
    {
        return await _context.Users
            .Select(u => new UserResponse
            {
                Id = u.Id,
                Username = u.Username,
                Role = u.Role
            })
            .ToListAsync();
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request, string currentJti)
    {
        // find user from db
        var user = await _context.Users.FindAsync(userId);
        if(user is null) throw new KeyNotFoundException("User not found");

        // validate old password
        bool isOldPasswordValid = BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash);
        if(!isOldPasswordValid) throw new ArgumentException("Wrong password.");

        // hash new password and save
        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        await _context.SaveChangesAsync();

        await InvalidateTokenAsync(currentJti);
    }

    public async Task DeleteUserAsync(Guid userId, string currentJti)
    {
        var user = await _context.Users.FindAsync(userId);
        if(user is null) throw new KeyNotFoundException("User not found");

        _context.Users.Remove(user);
        await _context.SaveChangesAsync();

        await InvalidateTokenAsync(currentJti);
    }

    private async Task InvalidateTokenAsync(string jti)
    {
        if(string.IsNullOrEmpty(jti)) return;

        var expirationMinutes = _configuration.GetValue<int>("JwtSettings:ExpirationMinutes");

        var cacheOptions = new DistributedCacheEntryOptions
        {
            // should stay in blacklist untill token lifetime ends
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expirationMinutes)
        };

        // save to cache as blacklist_ tokenID
        await _cache.SetStringAsync($"blacklist_{jti}", "revoked", cacheOptions);
    }

    private string GenerateJwtToken(User user)
    {
        // Take secret from SecretManager or AppSettings
        var secretKey = _configuration.GetValue<string>("JwtSettings:SecretKey");
        if(string.IsNullOrEmpty(secretKey)) throw new InvalidOperationException("JWT secret not found.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // identity claims to include inside ticket
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        // define the rules
        var tokenOptions = new JwtSecurityToken(
            issuer: _configuration.GetValue<string>("JwtSettings:Issuer"),
            audience: _configuration.GetValue<string>("JwtSettings:Audience"),
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_configuration.GetValue<int>("JwtSettings:ExpirationMinutes")),
            signingCredentials: creds
        );

        // stringify the ticket
        return new JwtSecurityTokenHandler().WriteToken(tokenOptions);
    }
}