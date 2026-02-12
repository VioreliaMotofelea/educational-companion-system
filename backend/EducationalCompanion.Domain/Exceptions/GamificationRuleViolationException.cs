namespace EducationalCompanion.Domain.Exceptions;

public class GamificationRuleViolationException : DomainException
{
    public GamificationRuleViolationException(string rule)
        : base($"Gamification rule violation: {rule}")
    {
    }
}