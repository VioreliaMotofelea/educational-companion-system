namespace EducationalCompanion.Domain.Exceptions;

public class GamificationEventNotFoundException : NotFoundException
{
    public GamificationEventNotFoundException(Guid id)
        : base("GamificationEvent", id)
    {
    }
}
