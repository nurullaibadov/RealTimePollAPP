using Microsoft.AspNetCore.Identity;

namespace RealTimePoll.Infrastructure.Identity;

/// <summary>
/// ASP.NET Core Identity entegrasyonu — sadece Infrastructure katmanında yaşar.
/// Domain AppUser ile senkronize tutulur (ortak Id üzerinden).
/// </summary>
public class AppIdentityUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Navigation (EF ilişkileri burada)
    public ICollection<AppIdentityRefreshToken> RefreshTokens { get; set; } = new List<AppIdentityRefreshToken>();

    public string FullName => $"{FirstName} {LastName}";
}
