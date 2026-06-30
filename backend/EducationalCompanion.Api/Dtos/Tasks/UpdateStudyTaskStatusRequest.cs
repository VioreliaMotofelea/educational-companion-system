using System.ComponentModel.DataAnnotations;

namespace EducationalCompanion.Api.Dtos.Tasks;

public record UpdateStudyTaskStatusRequest(
    [Required] string Status
);

