namespace EducationalCompanion.Domain.Exceptions;

public class UserProfileNotFoundException : NotFoundException
{
    public UserProfileNotFoundException(string userId)
        : base("UserProfile", userId)
    {
    }
}