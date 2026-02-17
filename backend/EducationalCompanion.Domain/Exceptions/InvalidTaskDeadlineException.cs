namespace EducationalCompanion.Domain.Exceptions;

public class InvalidTaskDeadlineException : ValidationException
{
    public InvalidTaskDeadlineException(DateTime deadline)
        : base($"Task deadline '{deadline:u}' is invalid. It must be in the future.")
    {
    }
}