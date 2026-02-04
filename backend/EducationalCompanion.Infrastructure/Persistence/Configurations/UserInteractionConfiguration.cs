using EducationalCompanion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EducationalCompanion.Infrastructure.Persistence.Configurations;

public class UserInteractionConfiguration : IEntityTypeConfiguration<UserInteraction>
{
    public void Configure(EntityTypeBuilder<UserInteraction> builder)
    {
        builder.ToTable("UserInteractions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.InteractionType)
            .IsRequired();

        builder.HasOne(x => x.LearningResource)
            .WithMany(r => r.Interactions)
            .HasForeignKey(x => x.LearningResourceId);
    }
}