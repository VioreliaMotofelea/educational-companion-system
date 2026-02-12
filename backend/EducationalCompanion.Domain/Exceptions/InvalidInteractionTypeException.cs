namespace EducationalCompanion.Domain.Exceptions;

public class InvalidInteractionTypeException : ValidationException
{
    public InvalidInteractionTypeException(string interactionType)
        : base($"Interaction type '{interactionType}' is invalid.")
    {
    }
}