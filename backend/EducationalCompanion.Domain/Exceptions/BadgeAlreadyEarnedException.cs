namespace EducationalCompanion.Domain.Exceptions;

public class BadgeAlreadyEarnedException : ConflictException
{
    public BadgeAlreadyEarnedException(string userId, Guid badgeId)
        : base($"User '{userId}' has already earned badge '{badgeId}'.")
    {
    }
}
