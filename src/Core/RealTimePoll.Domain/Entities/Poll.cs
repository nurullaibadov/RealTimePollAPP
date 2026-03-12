using RealTimePoll.Domain.Enums;

namespace RealTimePoll.Domain.Entities;

public class Poll : BaseEntity
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime StartsAt { get; set; }
    public DateTime EndsAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool AllowMultipleVotes { get; set; } = false;
    public bool IsAnonymous { get; set; } = false;
    public PollStatus Status { get; set; } = PollStatus.Draft;
    public string? ThumbnailUrl { get; set; }
    public int TotalVotes { get; set; } = 0;

    // Foreign Keys
    public Guid CreatedByUserId { get; set; }

    // Navigation (no nav to AppUser — Identity lives in Infrastructure)
    public ICollection<PollOption> Options { get; set; } = new List<PollOption>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
