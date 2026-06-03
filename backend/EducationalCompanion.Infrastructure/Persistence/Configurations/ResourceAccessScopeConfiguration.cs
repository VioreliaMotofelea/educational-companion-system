using EducationalCompanion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EducationalCompanion.Infrastructure.Persistence.Configurations;

public class ResourceAccessScopeConfiguration : IEntityTypeConfiguration<ResourceAccessScope>
{
    public void Configure(EntityTypeBuilder<ResourceAccessScope> builder)
    {
        builder.ToTable("ResourceAccessScopes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ScopeKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.LearningResourceId);
        builder.HasIndex(x => new { x.ScopeType, x.ScopeKey });
        builder.HasIndex(x => new { x.LearningResourceId, x.ScopeType, x.ScopeKey })
            .IsUnique();

        builder.HasOne(x => x.LearningResource)
            .WithMany(r => r.AccessScopes)
            .HasForeignKey(x => x.LearningResourceId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
