namespace EducationalCompanion.Domain.Exceptions;

public class RecommendationNotFoundException : NotFoundException
{
    public RecommendationNotFoundException(Guid id)
        : base("Recommendation", id)
    {
    }
}