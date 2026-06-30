using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.Auth;

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);
