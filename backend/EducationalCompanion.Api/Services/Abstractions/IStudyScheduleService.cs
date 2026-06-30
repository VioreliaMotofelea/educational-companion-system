using EducationalCompanion.Api.Dtos.Schedule;

namespace EducationalCompanion.Api.Services.Abstractions;

public interface IStudyScheduleService
{
    Task<StudyDayScheduleResponse> GetTodayScheduleAsync(string userId, DateOnly? date = null, CancellationToken ct = default);
}
