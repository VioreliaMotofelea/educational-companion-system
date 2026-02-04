using EducationalCompanion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EducationalCompanion.Infrastructure.Persistence.Configurations;

public class GamificationEventConfiguration : IEntityTypeConfiguration<GamificationEvent>
{
    public void Configure(EntityTypeBuilder<GamificationEvent> builder)
    {
        builder.ToTable("GamificationEvents");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.XpGranted)
            .IsRequired();
    }
}