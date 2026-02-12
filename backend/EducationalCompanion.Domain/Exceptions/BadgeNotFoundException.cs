namespace EducationalCompanion.Domain.Exceptions;

public class BadgeNotFoundException : NotFoundException
{
    public BadgeNotFoundException(Guid id)
        : base("Badge", id)
    {
    }
}
