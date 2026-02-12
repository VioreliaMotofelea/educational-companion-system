namespace EducationalCompanion.Domain.Exceptions;

public class InvalidDifficultyException : ValidationException
{
    public InvalidDifficultyException(int difficulty)
        : base($"Difficulty '{difficulty}' is invalid. Valid range is 1–5.")
    {
    }
}