using EducationalCompanion.Api.Dtos.Auth;
using System.Security.Claims;

namespace EducationalCompanion.Api.Services.Abstractions;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken ct);
    Task LogoutAsync(string refreshToken, CancellationToken ct);
    CurrentUserResponse GetCurrentUser(ClaimsPrincipal principal);
}
