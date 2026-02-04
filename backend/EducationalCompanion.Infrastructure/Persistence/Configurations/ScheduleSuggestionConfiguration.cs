using EducationalCompanion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EducationalCompanion.Infrastructure.Persistence.Configurations;

public class ScheduleSuggestionConfiguration : IEntityTypeConfiguration<ScheduleSuggestion>
{
    public void Configure(EntityTypeBuilder<ScheduleSuggestion> builder)
    {
        builder.ToTable("ScheduleSuggestions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Explanation)
            .HasMaxLength(1000);
    }
}