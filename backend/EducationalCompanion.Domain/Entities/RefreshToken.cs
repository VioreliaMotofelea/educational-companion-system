using EducationalCompanion.Domain.Common;

namespace EducationalCompanion.Domain.Entities;

public class RefreshToken : BaseEntity
{
    public string UserId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAtUtc { get; set; }
}
