namespace EducationalCompanion.Domain.Exceptions;

public class InvalidPriorityException : ValidationException
{
    public InvalidPriorityException(int priority)
        : base($"Priority '{priority}' is invalid. Valid range is 1–5.")
    {
    }
}
