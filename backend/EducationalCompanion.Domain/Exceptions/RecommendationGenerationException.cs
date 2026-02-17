namespace EducationalCompanion.Domain.Exceptions;

public class RecommendationGenerationException : DomainException
{
    public RecommendationGenerationException(string reason)
        : base($"Recommendation generation failed: {reason}")
    {
    }
}