namespace EducationalCompanion.Domain.Exceptions;

public class LearningResourceNotFoundException : NotFoundException
{
    public LearningResourceNotFoundException(Guid id)
        : base("LearningResource", id)
    {
    }
}