namespace EducationalCompanion.Api.Dtos.Auth;

public record CurrentUserResponse(
    string UserId,
    string Email
);
