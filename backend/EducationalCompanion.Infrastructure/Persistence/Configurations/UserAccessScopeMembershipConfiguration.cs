using EducationalCompanion.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EducationalCompanion.Infrastructure.Persistence.Configurations;

public class UserAccessScopeMembershipConfiguration : IEntityTypeConfiguration<UserAccessScopeMembership>
{
    public void Configure(EntityTypeBuilder<UserAccessScopeMembership> builder)
    {
        builder.ToTable("UserAccessScopeMemberships");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.UserId)
            .IsRequired()
            .HasMaxLength(128);

        builder.Property(x => x.ScopeKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => new { x.ScopeType, x.ScopeKey });
        builder.HasIndex(x => new { x.UserId, x.ScopeType, x.ScopeKey })
            .IsUnique();
    }
}
