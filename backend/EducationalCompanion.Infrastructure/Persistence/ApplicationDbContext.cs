using EducationalCompanion.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EducationalCompanion.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
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

    public DbSet<UserInteraction> UserInteractions => Set<UserInteraction>();

    public DbSet<Recommendation> Recommendations => Set<Recommendation>();

    public DbSet<GamificationEvent> GamificationEvents => Set<GamificationEvent>();
    public DbSet<Badge> Badges => Set<Badge>();
    public DbSet<UserBadge> UserBadges => Set<UserBadge>();

    public DbSet<StudyTask> StudyTasks => Set<StudyTask>();
    public DbSet<ScheduleSuggestion> ScheduleSuggestions => Set<ScheduleSuggestion>();

    // =========================
    // Model configuration
    // =========================

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}