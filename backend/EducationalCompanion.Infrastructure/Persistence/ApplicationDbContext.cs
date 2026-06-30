using EducationalCompanion.Domain.Common;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<AppUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // =========================
    // DbSets (Tables)
    // =========================

    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<UserPreferences> UserPreferences => Set<UserPreferences>();

    public DbSet<LearningResource> LearningResources => Set<LearningResource>();
    public DbSet<ResourceMetadata> ResourceMetadata => Set<ResourceMetadata>();
    public DbSet<ResourceAccessScope> ResourceAccessScopes => Set<ResourceAccessScope>();
    public DbSet<UserAccessScopeMembership> UserAccessScopeMemberships => Set<UserAccessScopeMembership>();

    public DbSet<UserInteraction> UserInteractions => Set<UserInteraction>();

    public DbSet<Recommendation> Recommendations => Set<Recommendation>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<GamificationEvent> GamificationEvents => Set<GamificationEvent>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();

    public DbSet<StudyTask> StudyTasks => Set<StudyTask>();
    public DbSet<ScheduleSuggestion> ScheduleSuggestions => Set<ScheduleSuggestion>();

    public DbSet<ResourceFile> ResourceFiles => Set<ResourceFile>();
    public DbSet<ResourceExtractedText> ResourceExtractedTexts => Set<ResourceExtractedText>();

    // =========================
    // Model configuration
    // =========================

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity.CreatedAtUtc == default)
                    entry.Entity.CreatedAtUtc = DateTime.UtcNow;
            }
            else if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}