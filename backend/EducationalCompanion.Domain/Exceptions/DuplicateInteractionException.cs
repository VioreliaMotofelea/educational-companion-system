namespace EducationalCompanion.Domain.Exceptions;

public class DuplicateInteractionException : ConflictException
{
    public DuplicateInteractionException(string userId, Guid resourceId)
        : base($"User '{userId}' already has an interaction recorded for resource '{resourceId}'.")
    {
    }
}