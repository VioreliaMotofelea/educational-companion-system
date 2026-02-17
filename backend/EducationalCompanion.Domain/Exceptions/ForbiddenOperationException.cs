namespace EducationalCompanion.Domain.Exceptions;

public class ForbiddenOperationException : DomainException
{
    public ForbiddenOperationException(string message) : base(message)
    {
    }
}