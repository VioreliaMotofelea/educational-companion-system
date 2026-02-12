namespace EducationalCompanion.Api.Common;

public class ErrorResponse
{
    public int Status { get; set; }
    public string Error { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}