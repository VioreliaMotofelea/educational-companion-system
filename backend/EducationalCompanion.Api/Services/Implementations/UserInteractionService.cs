using EducationalCompanion.Api.Dtos.UserInteractions;
using EducationalCompanion.Api.Services.Abstractions;
using EducationalCompanion.Domain.Entities;
using EducationalCompanion.Domain.Enums;
using EducationalCompanion.Domain.Exceptions;
using EducationalCompanion.Infrastructure.Repositories.Abstractions;

namespace EducationalCompanion.Api.Services.Implementations;

public class UserInteractionService : IUserInteractionService
{
    private const int MinRating = 1;
    private const int MaxRating = 5;

    private readonly IUserInteractionRepository _repo;
    private readonly ILearningResourceRepository _learningResourceRepo;
    private readonly IStudyTaskService _studyTaskService;

    public UserInteractionService(
        IUserInteractionRepository repo,
        ILearningResourceRepository learningResourceRepo,
        IStudyTaskService studyTaskService)
    {
        _repo = repo;
        _learningResourceRepo = learningResourceRepo;
        _studyTaskService = studyTaskService;
    }

    public async Task<IReadOnlyList<UserInteractionResponse>> GetAllAsync(CancellationToken ct)
    {
        var items = await _repo.GetAllAsync(ct);
        return items.Select(Map).ToList();
    }

    public async Task<UserInteractionResponse> GetByIdAsync(Guid id, CancellationToken ct)
    {
        var item = await _repo.GetByIdAsync(id, ct);
        if (item is null)
            throw new UserInteractionNotFoundException(id);
        return Map(item);
    }

    public async Task<IReadOnlyList<UserInteractionResponse>> GetByUserAsync(string userId, CancellationToken ct)
    {
        var items = await _repo.GetByUserAsync(userId, ct);
        return items.Select(Map).ToList();
    }

    public async Task<IReadOnlyList<UserInteractionResponse>> SearchAsync(
        string? userId,
        Guid? learningResourceId,
        string? interactionType,
        CancellationToken ct)
    {
        var items = await _repo.SearchAsync(userId, learningResourceId, interactionType, ct);
        return items.Select(Map).ToList();
    }

    public async Task<UserInteractionResponse> CreateAsync(CreateUserInteractionRequest request, CancellationToken ct)
    {
        await EnsureLearningResourceExistsAsync(request.LearningResourceId, ct);

        var interactionType = ParseInteractionType(request.InteractionType);
        ValidateRatingForCreate(interactionType, request.Rating);
        if (request.TimeSpentMinutes is int mins && mins < 0)
            throw new ValidationException("TimeSpentMinutes must be non-negative.");

        var entity = new UserInteraction
        {
            UserId = request.UserId,
            LearningResourceId = request.LearningResourceId,
            InteractionType = interactionType,
            Rating = request.Rating,
            TimeSpentMinutes = request.TimeSpentMinutes
        };

        await _repo.AddAsync(entity, ct);
        await _repo.SaveChangesAsync(ct);

        if (interactionType == InteractionType.Completed)
        {
            await _studyTaskService.MarkTaskCompletedForResourceAsync(
                request.UserId,
                request.LearningResourceId,
                ct);
        }

        return Map(entity);
    }

    public async Task UpdateAsync(Guid id, UpdateUserInteractionRequest request, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is null)
            throw new UserInteractionNotFoundException(id);

        if (request.InteractionType is { } typeStr)
        {
            existing.InteractionType = ParseInteractionType(typeStr);
        }

        if (request.Rating.HasValue)
        {
            if (request.Rating.Value < MinRating || request.Rating.Value > MaxRating)
                throw new InvalidRatingException(request.Rating.Value);
            existing.Rating = request.Rating;
        }

        if (request.TimeSpentMinutes.HasValue)
        {
            if (request.TimeSpentMinutes.Value < 0)
                throw new ValidationException("TimeSpentMinutes must be non-negative.");
            existing.TimeSpentMinutes = request.TimeSpentMinutes;
        }

        _repo.Update(existing);
        await _repo.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var existing = await _repo.GetByIdAsync(id, ct);
        if (existing is null)
            throw new UserInteractionNotFoundException(id);

        _repo.Remove(existing);
        await _repo.SaveChangesAsync(ct);
    }

    private async Task EnsureLearningResourceExistsAsync(Guid learningResourceId, CancellationToken ct)
    {
        var resource = await _learningResourceRepo.GetByIdAsync(learningResourceId, ct);
        if (resource is null)
            throw new LearningResourceNotFoundException(learningResourceId);
    }

    private static InteractionType ParseInteractionType(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !Enum.TryParse<InteractionType>(value, true, out var parsed))
            throw new InvalidInteractionTypeException(value ?? "(null)");
        return parsed;
    }

    private static void ValidateRatingForCreate(InteractionType interactionType, int? rating)
    {
        if (rating.HasValue)
        {
            if (rating.Value < MinRating || rating.Value > MaxRating)
                throw new InvalidRatingException(rating.Value);
        }
        else if (interactionType == InteractionType.Rated)
        {
            throw new ValidationException("Rating is required when InteractionType is Rated.");
        }
    }

    private static UserInteractionResponse Map(UserInteraction e) =>
        new(
            e.Id,
            e.UserId,
            e.LearningResourceId,
            e.InteractionType.ToString(),
            e.Rating,
            e.TimeSpentMinutes,
            e.CreatedAtUtc
        );
}
