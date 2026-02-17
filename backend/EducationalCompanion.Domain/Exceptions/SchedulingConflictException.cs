namespace EducationalCompanion.Domain.Exceptions;

public class SchedulingConflictException : ConflictException
{
    public SchedulingConflictException(string userId)
        : base($"Scheduling conflict detected for user '{userId}'.")
    {
    }
}