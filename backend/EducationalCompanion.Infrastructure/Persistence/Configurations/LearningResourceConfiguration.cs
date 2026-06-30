using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
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

        builder.Property(x => x.SourceName)
            .HasMaxLength(150);

        builder.Property(x => x.Url)
            .HasMaxLength(2048);

        builder.Property(x => x.AccessInstructions)
            .HasMaxLength(1000);

        builder.Property(x => x.AccessType)
            .HasDefaultValue(ResourceAccessType.NoDirectAccess);

        builder.Property(x => x.Visibility)
            .HasDefaultValue(ResourceVisibility.Global)
            .HasSentinel((ResourceVisibility)0);

        builder.Property(x => x.OwnerUserId)
            .HasMaxLength(128);

        builder.HasIndex(x => x.OwnerUserId);

        builder.HasIndex(x => x.Topic);
        builder.HasIndex(x => x.AccessType);
        builder.HasIndex(x => x.Visibility);

        builder.HasOne(x => x.Metadata)
            .WithOne(m => m.LearningResource)
            .HasForeignKey<ResourceMetadata>(m => m.LearningResourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}