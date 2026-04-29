using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EducationalCompanion.Api.Dtos.Auth;
using EducationalCompanion.Api.Options;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Identity;
using EducationalCompanion.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EducationalCompanion.Api.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ApplicationDbContext _dbContext;
    private readonly JwtOptions _jwtOptions;

    public AuthService(
        UserManager<AppUser> userManager,
        ApplicationDbContext dbContext,
        IOptions<JwtOptions> jwtOptions)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _jwtOptions = jwtOptions.Value;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _userManager.FindByEmailAsync(email);
        if (existingUser is not null)
            throw new ConflictException("An account with this email already exists.");

        var user = new AppUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
            throw new ValidationException(string.Join("; ", createResult.Errors.Select(e => e.Description)));

        var profile = new UserProfile
        {
            UserId = user.Id,
            DailyAvailableMinutes = request.DailyAvailableMinutes
        };
        _dbContext.UserProfiles.Add(profile);
        await _dbContext.SaveChangesAsync(ct);

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null || !await _userManager.CheckPasswordAsync(user, request.Password))
            throw new ValidationException("Invalid email or password.");

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct)
    {
        var tokenHash = HashRefreshToken(request.RefreshToken);
        var now = DateTime.UtcNow;

        var stored = await _dbContext.RefreshTokens
            .Where(t => t.TokenHash == tokenHash)
            .FirstOrDefaultAsync(ct);

        if (stored is null || stored.RevokedAtUtc.HasValue || stored.ExpiresAtUtc <= now)
            throw new ValidationException("Refresh token is invalid or expired.");

        stored.RevokedAtUtc = now;

        var user = await _userManager.FindByIdAsync(stored.UserId);
        if (user is null)
            throw new ValidationException("User account no longer exists.");

        return await BuildAuthResponseAsync(user, ct);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var tokenHash = HashRefreshToken(refreshToken);
        var stored = await _dbContext.RefreshTokens
            .Where(t => t.TokenHash == tokenHash)
            .FirstOrDefaultAsync(ct);

        if (stored is null || stored.RevokedAtUtc.HasValue)
            return;

        stored.RevokedAtUtc = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(ct);
    }

    public CurrentUserResponse GetCurrentUser(ClaimsPrincipal principal)
    {
        var userId = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var email = principal.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? principal.FindFirstValue(ClaimTypes.Email)
            ?? string.Empty;

        if (string.IsNullOrWhiteSpace(userId))
            throw new ValidationException("Missing authenticated user identifier.");

        return new CurrentUserResponse(userId, email);
    }

    private async Task<AuthResponse> BuildAuthResponseAsync(AppUser user, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var accessExpiresAt = now.AddMinutes(_jwtOptions.AccessTokenMinutes);
        var refreshExpiresAt = now.AddDays(_jwtOptions.RefreshTokenDays);

        var accessToken = BuildAccessToken(user, accessExpiresAt);
        var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
        var tokenHash = HashRefreshToken(rawRefreshToken);

        // Revoke active refresh tokens for this user for single-session semantics.
        var activeTokens = await _dbContext.RefreshTokens
            .Where(t => t.UserId == user.Id && !t.RevokedAtUtc.HasValue && t.ExpiresAtUtc > now)
            .ToListAsync(ct);
        foreach (var active in activeTokens)
            active.RevokedAtUtc = now;

        _dbContext.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAtUtc = refreshExpiresAt,
            CreatedAtUtc = now
        });
        await _dbContext.SaveChangesAsync(ct);

        return new AuthResponse(
            accessToken,
            accessExpiresAt,
            rawRefreshToken,
            refreshExpiresAt,
            user.Id,
            user.Email ?? string.Empty
        );
    }

    private string BuildAccessToken(AppUser user, DateTime expiresAtUtc)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string HashRefreshToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
