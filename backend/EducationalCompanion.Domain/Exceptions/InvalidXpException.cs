namespace EducationalCompanion.Domain.Exceptions;

public class InvalidXpException : ValidationException
{
    public InvalidXpException(int xp)
        : base($"XP value '{xp}' is invalid. XP must be non-negative.")
    {
    }
}
