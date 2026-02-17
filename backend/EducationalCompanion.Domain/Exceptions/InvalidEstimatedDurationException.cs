namespace EducationalCompanion.Domain.Exceptions;

public class InvalidEstimatedDurationException : ValidationException
{
    public InvalidEstimatedDurationException(int minutes)
        : base($"Estimated duration '{minutes}' minutes is invalid. Must be a positive number.")
    {
    }
}
