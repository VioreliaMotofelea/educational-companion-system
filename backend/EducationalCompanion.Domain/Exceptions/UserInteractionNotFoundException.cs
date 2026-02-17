namespace EducationalCompanion.Domain.Exceptions;

public class UserInteractionNotFoundException : NotFoundException
{
    public UserInteractionNotFoundException(Guid id)
        : base("UserInteraction", id)
    {
    }
}