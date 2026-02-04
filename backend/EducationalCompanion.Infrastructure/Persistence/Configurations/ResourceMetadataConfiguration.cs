using EducationalCompanion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EducationalCompanion.Infrastructure.Persistence.Configurations;

public class ResourceMetadataConfiguration : IEntityTypeConfiguration<ResourceMetadata>
{
    public void Configure(EntityTypeBuilder<ResourceMetadata> builder)
    {
        builder.ToTable("ResourceMetadata");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Keywords)
            .IsRequired()
            .HasMaxLength(500);
    }
}