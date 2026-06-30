using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.Users;

public record UpdateUserStudySettingsRequest(
    [param: Range(15, 600)] int DailyAvailableMinutes
);
