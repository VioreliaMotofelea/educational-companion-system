using Microsoft.EntityFrameworkCore;

using EducationalCompanion.Infrastructure.Persistence;
using EducationalCompanion.Infrastructure.Persistence.Seed;

using EducationalCompanion.Infrastructure.Repositories.Abstractions;
using EducationalCompanion.Infrastructure.Repositories.Implementations;

using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Api.Services.Implementations;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// DbContext (PostgreSQL)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
    options.UseNpgsql(connectionString);
});

// Repositories
builder.Services.AddScoped<ILearningResourceRepository, LearningResourceRepository>();

// Services
builder.Services.AddScoped<ILearningResourceService, LearningResourceService>();

var app = builder.Build();

// Apply migrations and seed database (Development only)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.MigrateAsync();
    await DatabaseSeeder.SeedAsync(dbContext);
}

// Middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.MapControllers();

app.Run();
