namespace EducationalCompanion.Domain.Exceptions;

public class InvalidRatingException : ValidationException
{
    public InvalidRatingException(int rating)
        : base($"Rating '{rating}' is invalid. Valid range is 1–5.")
    {
    }
}
