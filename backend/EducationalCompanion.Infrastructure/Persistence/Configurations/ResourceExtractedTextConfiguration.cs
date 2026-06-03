using EducationalCompanion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EducationalCompanion.Infrastructure.Persistence.Configurations;

public class ResourceExtractedTextConfiguration : IEntityTypeConfiguration<ResourceExtractedText>
{
    public void Configure(EntityTypeBuilder<ResourceExtractedText> builder)
    {
        builder.ToTable("ResourceExtractedTexts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExtractedText)
            .IsRequired()
            .HasMaxLength(50000);

        builder.Property(x => x.Summary)
            .HasMaxLength(2000);

        builder.HasIndex(x => x.LearningResourceId);
        builder.HasIndex(x => x.ResourceFileId);

        builder.HasOne(x => x.LearningResource)
            .WithMany(r => r.ExtractedTexts)
            .HasForeignKey(x => x.LearningResourceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.ResourceFile)
            .WithMany()
            .HasForeignKey(x => x.ResourceFileId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
