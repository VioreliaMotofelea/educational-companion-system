namespace EducationalCompanion.Domain.Exceptions;

public class ScheduleSuggestionNotFoundException : NotFoundException
{
    public ScheduleSuggestionNotFoundException(Guid id)
        : base("ScheduleSuggestion", id)
    {
    }
}
