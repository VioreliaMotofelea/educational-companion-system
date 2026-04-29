using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.Auth;

public record RefreshTokenRequest(
    [Required] string RefreshToken
);
