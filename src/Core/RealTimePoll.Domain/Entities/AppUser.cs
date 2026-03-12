namespace RealTimePoll.Domain.Entities;

/// <summary>
/// Saf domain entity — Identity bağımlılığı yok.
/// Identity entegrasyonu Infrastructure katmanındaki AppIdentityUser'da.
/// </summary>
public class AppUser : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public ICollection<Poll> CreatedPolls { get; set; } = new List<Poll>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();

    public string FullName => $"{FirstName} {LastName}";
}
