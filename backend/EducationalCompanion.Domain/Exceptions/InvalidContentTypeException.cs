namespace EducationalCompanion.Domain.Exceptions;

public class InvalidContentTypeException : ValidationException
{
    public InvalidContentTypeException(string contentType)
        : base($"Content type '{contentType}' is invalid. Allowed values: Article, Video, Quiz.")
    {
    }
}