namespace EducationalCompanion.Domain.Exceptions;

public class InvalidLevelException : ValidationException
{
    public InvalidLevelException(int level)
        : base($"Level '{level}' is invalid. Level must be at least 1.")
    {
    }
}
