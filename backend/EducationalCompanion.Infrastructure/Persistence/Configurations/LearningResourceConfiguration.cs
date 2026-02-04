using EducationalCompanion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EducationalCompanion.Infrastructure.Persistence.Configurations;

public class LearningResourceConfiguration : IEntityTypeConfiguration<LearningResource>
{
    public void Configure(EntityTypeBuilder<LearningResource> builder)
    {
        builder.ToTable("LearningResources");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Topic)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasOne(x => x.Metadata)
            .WithOne(m => m.LearningResource)
            .HasForeignKey<ResourceMetadata>(m => m.LearningResourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}