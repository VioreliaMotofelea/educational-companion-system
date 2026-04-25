using Microsoft.EntityFrameworkCore;

using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Persistence.Seed;

using EducationalCompanion.Infrastructure.Edm;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using EducationalCompanion.Infrastructure.Repositories.Implementations;

using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Api.Services.Implementations;
using EducationalCompanion.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy => policy
            .WithOrigins(allowedOrigins!)
            .AllowAnyHeader()
            .AllowAnyMethod());
});

// DbContext (PostgreSQL)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

// Repositories
builder.Services.AddScoped<ILearningResourceRepository, LearningResourceRepository>();
builder.Services.AddScoped<IUserInteractionRepository, UserInteractionRepository>();
builder.Services.AddScoped<IUserProfileRepository, UserProfileRepository>();
builder.Services.AddScoped<IUserPreferencesRepository, UserPreferencesRepository>();
builder.Services.AddScoped<IRecommendationRepository, RecommendationRepository>();
builder.Services.AddScoped<IUserEdmReadRepository, UserEdmReadRepository>();

// Services
builder.Services.AddScoped<ILearningResourceService, LearningResourceService>();
builder.Services.AddScoped<IUserInteractionService, UserInteractionService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IUserEdmService, UserEdmService>();
builder.Services.AddScoped<IRecommendationService, RecommendationService>();

var app = builder.Build();

// Global exception handling (early in pipeline)
app.UseMiddleware<ExceptionMiddleware>();

// Apply migrations and seed database (Development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(dbContext);

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
