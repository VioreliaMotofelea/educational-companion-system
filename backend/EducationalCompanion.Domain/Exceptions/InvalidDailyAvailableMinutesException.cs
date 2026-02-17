namespace EducationalCompanion.Domain.Exceptions;

public class InvalidDailyAvailableMinutesException : ValidationException
{
    public InvalidDailyAvailableMinutesException(int minutes)
        : base($"Daily available minutes '{minutes}' is invalid. Must be a positive number.")
    {
    }
}
