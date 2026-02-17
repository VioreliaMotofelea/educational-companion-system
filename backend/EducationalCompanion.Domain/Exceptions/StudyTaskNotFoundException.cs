namespace EducationalCompanion.Domain.Exceptions;

public class StudyTaskNotFoundException : NotFoundException
{
    public StudyTaskNotFoundException(Guid id)
        : base("StudyTask", id)
    {
    }
}