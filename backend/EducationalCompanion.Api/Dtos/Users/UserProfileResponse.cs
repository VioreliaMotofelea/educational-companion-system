namespace EducationalCompanion.Api.Dtos.Users;

public record UserProfileResponse(
    string UserId,
    int Level,
    int Xp,
    int DailyAvailableMinutes,
    UserPreferencesResponse? Preferences
);
