using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using TaskStatusEnum = EducationalCompanion.Domain.Enums.TaskStatus;

namespace EducationalCompanion.Infrastructure.Persistence.Seed;

public static class DatabaseSeeder
{
    /// Seeds the database with test data for all entities. Call after migrations have been applied.
    /// Idempotent: only inserts when each entity set is empty.
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // =========================
        // 1. BADGES (no dependencies)
        // =========================
        if (!await context.Badges.AnyAsync())
        {
            var badges = new List<Badge>
            {
                new() { Name = "First Steps", Description = "Completed first learning resource", RuleDefinition = "Complete 1 resource" },
                new() { Name = "Focused Learner", Description = "Completed a resource longer than 60 minutes", RuleDefinition = "TimeSpent > 60" },
                new() { Name = "Consistent Learner", Description = "Completed 5 learning resources", RuleDefinition = "Complete 5 resources" },
                new() { Name = "Streak Master", Description = "7-day study streak", RuleDefinition = "Streak 7 days" },
                new() { Name = "Early Bird", Description = "Completed task before deadline", RuleDefinition = "EarlyCompletion" },
                new() { Name = "Topic Explorer", Description = "Completed resources in 3 different topics", RuleDefinition = "3 topics" },
                new() { Name = "Video Enthusiast", Description = "Completed 5 video resources", RuleDefinition = "5 videos" }
            };
            context.Badges.AddRange(badges);
            await context.SaveChangesAsync();
        }

        // =========================
        // 2. LEARNING RESOURCES (no dependencies)
        // =========================
        if (!await context.LearningResources.AnyAsync())
        {
            var resources = new List<LearningResource>
            {
                new() { Title = "Introduction to Python", Description = "Basic Python syntax and variables", Topic = "Python", Difficulty = 1, EstimatedDurationMinutes = 45, ContentType = ResourceContentType.Article },
                new() { Title = "Python Control Flow", Description = "If statements, loops, and conditions", Topic = "Python", Difficulty = 2, EstimatedDurationMinutes = 50, ContentType = ResourceContentType.Video },
                new() { Title = "Advanced Python Functions", Description = "Decorators, lambda expressions, closures", Topic = "Python", Difficulty = 4, EstimatedDurationMinutes = 70, ContentType = ResourceContentType.Article },
                new() { Title = "Python Data Structures", Description = "Lists, dicts, sets, tuples", Topic = "Python", Difficulty = 2, EstimatedDurationMinutes = 55, ContentType = ResourceContentType.Article },
                new() { Title = "C# Basics", Description = "Syntax, types, and basic OOP", Topic = "C#", Difficulty = 1, EstimatedDurationMinutes = 40, ContentType = ResourceContentType.Video },
                new() { Title = "Object-Oriented Programming in C#", Description = "Inheritance, interfaces, polymorphism", Topic = "C#", Difficulty = 2, EstimatedDurationMinutes = 60, ContentType = ResourceContentType.Video },
                new() { Title = "Entity Framework Core Fundamentals", Description = "DbContext, DbSet, migrations", Topic = "Databases", Difficulty = 3, EstimatedDurationMinutes = 55, ContentType = ResourceContentType.Article },
                new() { Title = "SQL Basics", Description = "SELECT, INSERT, UPDATE, DELETE", Topic = "Databases", Difficulty = 1, EstimatedDurationMinutes = 35, ContentType = ResourceContentType.Article },
                new() { Title = "SQL Joins Explained", Description = "INNER, LEFT, RIGHT joins", Topic = "Databases", Difficulty = 2, EstimatedDurationMinutes = 45, ContentType = ResourceContentType.Video },
                new() { Title = "Database Indexing", Description = "Indexes, query plans, performance", Topic = "Databases", Difficulty = 4, EstimatedDurationMinutes = 50, ContentType = ResourceContentType.Article },
                new() { Title = "REST API Concepts", Description = "HTTP methods, status codes, REST principles", Topic = "Web", Difficulty = 2, EstimatedDurationMinutes = 40, ContentType = ResourceContentType.Article },
                new() { Title = "ASP.NET Core Web API", Description = "Controllers, routing, dependency injection", Topic = "Web", Difficulty = 3, EstimatedDurationMinutes = 60, ContentType = ResourceContentType.Video },
                new() { Title = "JWT Authentication", Description = "Tokens, claims, secure APIs", Topic = "Web", Difficulty = 3, EstimatedDurationMinutes = 45, ContentType = ResourceContentType.Article },
                new() { Title = "Machine Learning Basics", Description = "Supervised vs unsupervised learning", Topic = "AI", Difficulty = 2, EstimatedDurationMinutes = 50, ContentType = ResourceContentType.Article },
                new() { Title = "Recommender Systems Overview", Description = "Content-based and collaborative filtering", Topic = "AI", Difficulty = 3, EstimatedDurationMinutes = 55, ContentType = ResourceContentType.Article },
                new() { Title = "Neural Networks Introduction", Description = "Perceptrons, layers, backpropagation", Topic = "AI", Difficulty = 4, EstimatedDurationMinutes = 65, ContentType = ResourceContentType.Video },
                new() { Title = "Clean Code Principles", Description = "Readability, naming, refactoring", Topic = "Software Engineering", Difficulty = 2, EstimatedDurationMinutes = 40, ContentType = ResourceContentType.Article },
                new() { Title = "Unit Testing in C#", Description = "xUnit, mocks, test design", Topic = "Software Engineering", Difficulty = 3, EstimatedDurationMinutes = 50, ContentType = ResourceContentType.Video }
            };
            context.LearningResources.AddRange(resources);
            await context.SaveChangesAsync();
        }

        // =========================
        // 3. RESOURCE METADATA (depends on LearningResources)
        // =========================
        if (!await context.ResourceMetadata.AnyAsync())
        {
            var resources = await context.LearningResources.ToListAsync();
            var metadataList = new List<ResourceMetadata>();
            foreach (var r in resources)
            {
                var keywords = $"{r.Topic}, {r.Title}, programming, learning, {r.ContentType.ToString().ToLowerInvariant()}";
                metadataList.Add(new ResourceMetadata
                {
                    LearningResourceId = r.Id,
                    Keywords = keywords.Length > 500 ? keywords[..500] : keywords,
                    EmbeddingVectorJson = null // Placeholder for vector embeddings in recommender
                });
            }
            context.ResourceMetadata.AddRange(metadataList);
            await context.SaveChangesAsync();
        }

        // =========================
        // 4. USER PROFILES + PREFERENCES (no dependency on other custom entities)
        // =========================
        if (!await context.UserProfiles.AnyAsync())
        {
            var user1 = new UserProfile
            {
                UserId = "user-1",
                Level = 2,
                Xp = 120,
                DailyAvailableMinutes = 60,
                Preferences = new UserPreferences { PreferredDifficulty = 2, PreferredTopicsCsv = "Python,AI", PreferredContentTypesCsv = "Article,Video" }
            };
            var user2 = new UserProfile
            {
                UserId = "user-2",
                Level = 3,
                Xp = 260,
                DailyAvailableMinutes = 90,
                Preferences = new UserPreferences { PreferredDifficulty = 3, PreferredTopicsCsv = "C#,Databases,Web", PreferredContentTypesCsv = "Video" }
            };
            var user3 = new UserProfile
            {
                UserId = "user-3",
                Level = 1,
                Xp = 40,
                DailyAvailableMinutes = 45,
                Preferences = new UserPreferences { PreferredDifficulty = 1, PreferredTopicsCsv = "Python,Web", PreferredContentTypesCsv = "Article" }
            };
            var user4 = new UserProfile
            {
                UserId = "user-4",
                Level = 4,
                Xp = 450,
                DailyAvailableMinutes = 120,
                Preferences = new UserPreferences { PreferredDifficulty = 3, PreferredTopicsCsv = "AI,Software Engineering", PreferredContentTypesCsv = "Article,Video,Quiz" }
            };
            var user5 = new UserProfile
            {
                UserId = "user-5",
                Level = 2,
                Xp = 180,
                DailyAvailableMinutes = 75,
                Preferences = new UserPreferences { PreferredDifficulty = 2, PreferredTopicsCsv = "Databases,Web", PreferredContentTypesCsv = "Video" }
            };
            context.UserProfiles.AddRange(user1, user2, user3, user4, user5);
            await context.SaveChangesAsync();
        }

        // Load profiles and resources for relationships (by UserId and by title)
        var profilesByUserId = await context.UserProfiles.ToDictionaryAsync(p => p.UserId);
        var resourcesByTitle = await context.LearningResources.ToDictionaryAsync(r => r.Title);

        // =========================
        // 5. USER INTERACTIONS (UserProfile, LearningResource)
        // =========================
        if (!await context.UserInteractions.AnyAsync())
        {
            var interactions = new List<UserInteraction>();
            void AddInteraction(string userId, string resourceTitle, InteractionType type, int? rating, int? timeSpent)
            {
                if (!profilesByUserId.TryGetValue(userId, out var profile) || !resourcesByTitle.TryGetValue(resourceTitle, out var resource)) return;
                interactions.Add(new UserInteraction
                {
                    UserId = userId,
                    UserProfile = profile,
                    LearningResourceId = resource.Id,
                    LearningResource = resource,
                    InteractionType = type,
                    Rating = rating,
                    TimeSpentMinutes = timeSpent
                });
            }

            AddInteraction("user-1", "Introduction to Python", InteractionType.Completed, 5, 50);
            AddInteraction("user-1", "SQL Basics", InteractionType.Viewed, null, 15);
            AddInteraction("user-1", "Python Control Flow", InteractionType.Completed, 4, 55);
            AddInteraction("user-1", "REST API Concepts", InteractionType.Viewed, null, 20);
            AddInteraction("user-1", "Machine Learning Basics", InteractionType.Completed, 5, 52);
            AddInteraction("user-2", "Object-Oriented Programming in C#", InteractionType.Completed, 4, 65);
            AddInteraction("user-2", "SQL Joins Explained", InteractionType.Completed, 5, 48);
            AddInteraction("user-2", "ASP.NET Core Web API", InteractionType.Viewed, null, 30);
            AddInteraction("user-2", "Entity Framework Core Fundamentals", InteractionType.Completed, 4, 58);
            AddInteraction("user-2", "C# Basics", InteractionType.Completed, 5, 42);
            AddInteraction("user-3", "Introduction to Python", InteractionType.Viewed, null, 20);
            AddInteraction("user-3", "SQL Basics", InteractionType.Completed, 4, 38);
            AddInteraction("user-3", "REST API Concepts", InteractionType.Rated, 3, 35);
            AddInteraction("user-4", "Recommender Systems Overview", InteractionType.Completed, 5, 60);
            AddInteraction("user-4", "Neural Networks Introduction", InteractionType.Completed, 4, 70);
            AddInteraction("user-4", "Machine Learning Basics", InteractionType.Completed, 5, 48);
            AddInteraction("user-4", "Unit Testing in C#", InteractionType.Viewed, null, 25);
            AddInteraction("user-4", "Clean Code Principles", InteractionType.Completed, 5, 42);
            AddInteraction("user-5", "SQL Joins Explained", InteractionType.Completed, 4, 46);
            AddInteraction("user-5", "Database Indexing", InteractionType.Skipped, null, 5);
            AddInteraction("user-5", "JWT Authentication", InteractionType.Viewed, null, 20);

            context.UserInteractions.AddRange(interactions);
            await context.SaveChangesAsync();
        }

        // =========================
        // 6. RECOMMENDATIONS (UserId, LearningResource)
        // =========================
        if (!await context.Recommendations.AnyAsync())
        {
            var recommendations = new List<Recommendation>();
            void AddRec(string userId, string resourceTitle, double score, string algorithm, string explanation)
            {
                if (!resourcesByTitle.TryGetValue(resourceTitle, out var resource)) return;
                recommendations.Add(new Recommendation
                {
                    UserId = userId,
                    LearningResourceId = resource.Id,
                    LearningResource = resource,
                    Score = score,
                    AlgorithmUsed = algorithm,
                    Explanation = explanation
                });
            }

            AddRec("user-1", "Python Data Structures", 0.92, "ContentBased", "Matches your interest in Python and article content.");
            AddRec("user-1", "Recommender Systems Overview", 0.88, "ContentBased", "Topic AI and difficulty align with your profile.");
            AddRec("user-1", "Neural Networks Introduction", 0.75, "CollaborativeFiltering", "Users with similar history liked this.");
            AddRec("user-2", "JWT Authentication", 0.90, "ContentBased", "Fits your Web and C# focus.");
            AddRec("user-2", "Database Indexing", 0.85, "ContentBased", "Same topic as your completed Database resources.");
            AddRec("user-2", "Unit Testing in C#", 0.82, "Hybrid", "Content + popularity score.");
            AddRec("user-3", "Python Control Flow", 0.95, "ContentBased", "Next step after Introduction to Python.");
            AddRec("user-3", "C# Basics", 0.78, "ContentBased", "Beginner level and video format.");
            AddRec("user-4", "Advanced Python Functions", 0.80, "ContentBased", "Higher difficulty in Python topic.");
            AddRec("user-4", "Entity Framework Core Fundamentals", 0.77, "CollaborativeFiltering", "Similar users completed this.");
            AddRec("user-5", "Entity Framework Core Fundamentals", 0.88, "ContentBased", "Databases topic and video preference.");
            AddRec("user-5", "ASP.NET Core Web API", 0.86, "ContentBased", "Web topic and video format.");
            AddRec("user-5", "REST API Concepts", 0.82, "ContentBased", "Prerequisite for Web API course.");

            context.Recommendations.AddRange(recommendations);
            await context.SaveChangesAsync();
        }

        // =========================
        // 7. GAMIFICATION EVENTS (UserProfile)
        // =========================
        if (!await context.GamificationEvents.AnyAsync())
        {
            var events = new List<GamificationEvent>();
            void AddEvent(string userId, GamificationEventType type, int xp)
            {
                if (!profilesByUserId.TryGetValue(userId, out var profile)) return;
                events.Add(new GamificationEvent { UserId = userId, UserProfile = profile, EventType = type, XpGranted = xp });
            }

            AddEvent("user-1", GamificationEventType.CompletedResource, 10);
            AddEvent("user-1", GamificationEventType.CompletedResource, 10);
            AddEvent("user-1", GamificationEventType.StreakAchieved, 25);
            AddEvent("user-2", GamificationEventType.CompletedResource, 10);
            AddEvent("user-2", GamificationEventType.EarlyCompletion, 15);
            AddEvent("user-2", GamificationEventType.CompletedResource, 10);
            AddEvent("user-2", GamificationEventType.CompletedResource, 10);
            AddEvent("user-3", GamificationEventType.CompletedResource, 10);
            AddEvent("user-4", GamificationEventType.CompletedResource, 10);
            AddEvent("user-4", GamificationEventType.CompletedResource, 10);
            AddEvent("user-4", GamificationEventType.CompletedResource, 10);
            AddEvent("user-4", GamificationEventType.StreakAchieved, 25);
            AddEvent("user-4", GamificationEventType.EarlyCompletion, 15);
            AddEvent("user-5", GamificationEventType.CompletedResource, 10);
            AddEvent("user-5", GamificationEventType.CompletedResource, 10);

            context.GamificationEvents.AddRange(events);
            await context.SaveChangesAsync();
        }

        // =========================
        // 8. USER BADGES (UserProfile, Badge)
        // =========================
        if (!await context.UserBadges.AnyAsync())
        {
            var badges = await context.Badges.ToListAsync();
            var userBadges = new List<UserBadge>();
            void GrantBadge(string userId, int badgeIndex)
            {
                if (badgeIndex < 0 || badgeIndex >= badges.Count) return;
                if (!profilesByUserId.TryGetValue(userId, out var profile)) return;
                userBadges.Add(new UserBadge { UserId = userId, UserProfile = profile, BadgeId = badges[badgeIndex].Id, Badge = badges[badgeIndex] });
            }

            GrantBadge("user-1", 0); GrantBadge("user-1", 1); GrantBadge("user-1", 2); // First Steps, Focused Learner, Consistent Learner
            GrantBadge("user-2", 0); GrantBadge("user-2", 1); GrantBadge("user-2", 4); // First Steps, Focused Learner, Early Bird
            GrantBadge("user-3", 0);
            GrantBadge("user-4", 0); GrantBadge("user-4", 1); GrantBadge("user-4", 2); GrantBadge("user-4", 3); GrantBadge("user-4", 5);
            GrantBadge("user-5", 0); GrantBadge("user-5", 1);

            context.UserBadges.AddRange(userBadges);
            await context.SaveChangesAsync();
        }

        // =========================
        // 9. STUDY TASKS (UserProfile, optional LearningResource)
        // =========================
        if (!await context.StudyTasks.AnyAsync())
        {
            var tasks = new List<StudyTask>();
            void AddTask(string userId, string title, string? resourceTitle, int daysFromNow, int estMin, int priority, TaskStatusEnum status)
            {
                if (!profilesByUserId.TryGetValue(userId, out var profile)) return;
                Guid? resourceId = null;
                LearningResource? resource = null;
                if (resourceTitle != null && resourcesByTitle.TryGetValue(resourceTitle, out resource))
                    resourceId = resource.Id;
                tasks.Add(new StudyTask
                {
                    UserId = userId,
                    UserProfile = profile,
                    Title = title,
                    LearningResourceId = resourceId,
                    LearningResource = resource,
                    DeadlineUtc = DateTime.UtcNow.AddDays(daysFromNow),
                    EstimatedMinutes = estMin,
                    Priority = priority,
                    Status = status
                });
            }

            AddTask("user-1", "Finish Python Control Flow", "Python Control Flow", 3, 60, 3, TaskStatusEnum.Pending);
            AddTask("user-1", "Review REST API", null, 5, 30, 2, TaskStatusEnum.Pending);
            AddTask("user-1", "Complete SQL Basics", "SQL Basics", -1, 35, 4, TaskStatusEnum.Completed);
            AddTask("user-2", "Review SQL Joins", "SQL Joins Explained", 2, 45, 4, TaskStatusEnum.Pending);
            AddTask("user-2", "Watch EF Core video", "Entity Framework Core Fundamentals", 7, 55, 3, TaskStatusEnum.Pending);
            AddTask("user-2", "C# OOP practice", "Object-Oriented Programming in C#", -2, 60, 5, TaskStatusEnum.Overdue);
            AddTask("user-3", "Complete Python intro", "Introduction to Python", 1, 45, 5, TaskStatusEnum.Pending);
            AddTask("user-3", "Start SQL", "SQL Basics", 4, 35, 3, TaskStatusEnum.Pending);
            AddTask("user-4", "Neural networks deep dive", "Neural Networks Introduction", 2, 65, 4, TaskStatusEnum.Pending);
            AddTask("user-4", "Unit testing lab", "Unit Testing in C#", 10, 50, 3, TaskStatusEnum.Pending);
            AddTask("user-5", "Database indexing", "Database Indexing", 1, 50, 4, TaskStatusEnum.Pending);
            AddTask("user-5", "JWT auth tutorial", "JWT Authentication", 5, 45, 3, TaskStatusEnum.Pending);

            context.StudyTasks.AddRange(tasks);
            await context.SaveChangesAsync();
        }

        // =========================
        // 10. SCHEDULE SUGGESTIONS (StudyTask)
        // =========================
        if (!await context.ScheduleSuggestions.AnyAsync())
        {
            var tasks = await context.StudyTasks.Where(t => t.Status == TaskStatusEnum.Pending).Take(8).ToListAsync();
            var suggestions = new List<ScheduleSuggestion>();
            var baseDate = DateTime.UtcNow.Date;
            for (var i = 0; i < tasks.Count; i++)
            {
                var task = tasks[i];
                suggestions.Add(new ScheduleSuggestion
                {
                    UserId = task.UserId,
                    StudyTaskId = task.Id,
                    StudyTask = task,
                    SuggestedDateUtc = baseDate.AddDays(i % 3).AddHours(9 + (i % 2) * 3),
                    SuggestedDurationMinutes = Math.Min(task.EstimatedMinutes, 60),
                    Explanation = $"Suggested based on your daily availability and task priority. Best slot for focused work."
                });
            }
            context.ScheduleSuggestions.AddRange(suggestions);
            await context.SaveChangesAsync();
        }
    }
}
