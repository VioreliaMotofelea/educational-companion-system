namespace EducationalCompanion.Domain.Exceptions;

public class UserBadgeNotFoundException : NotFoundException
{
    public UserBadgeNotFoundException(Guid id)
        : base("UserBadge", id)
    {
    }
}
