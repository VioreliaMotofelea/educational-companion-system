namespace EducationalCompanion.Domain.Exceptions;

public class TaskAlreadyCompletedException : ConflictException
{
    public TaskAlreadyCompletedException(Guid taskId)
        : base($"Task '{taskId}' has already been completed.")
    {
    }
}
