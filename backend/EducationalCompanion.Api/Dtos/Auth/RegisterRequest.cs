using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.Auth;

public record RegisterRequest(
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    [Range(15, 600)] int DailyAvailableMinutes
);
