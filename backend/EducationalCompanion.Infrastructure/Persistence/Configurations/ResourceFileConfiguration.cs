using EducationalCompanion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EducationalCompanion.Infrastructure.Persistence.Configurations;

public class ResourceFileConfiguration : IEntityTypeConfiguration<ResourceFile>
{
    public void Configure(EntityTypeBuilder<ResourceFile> builder)
    {
        builder.ToTable("ResourceFiles");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OriginalFileName)
            .IsRequired()
            .HasMaxLength(255);

        builder.Property(x => x.StorageKey)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.MimeType)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.UploadedByUserId)
            .HasMaxLength(128);

        builder.Property(x => x.ProcessingError)
            .HasMaxLength(500);

        builder.HasIndex(x => x.LearningResourceId);
        builder.HasIndex(x => x.StorageKey)
            .IsUnique();

        builder.HasOne(x => x.LearningResource)
            .WithMany(r => r.ResourceFiles)
            .HasForeignKey(x => x.LearningResourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
