namespace EducationalCompanion.Domain.Exceptions;

public class UserPreferencesNotFoundException : NotFoundException
{
    public UserPreferencesNotFoundException(Guid userProfileId)
        : base("UserPreferences", userProfileId)
    {
    }
}
