namespace EducationalCompanion.Domain.Exceptions;

public class ResourceMetadataNotFoundException : NotFoundException
{
    public ResourceMetadataNotFoundException(Guid id)
        : base("ResourceMetadata", id)
    {
    }
}
