namespace RealTimePoll.Domain.Entities;

public class Vote : BaseEntity
{
    public Guid PollId { get; set; }
    public Guid PollOptionId { get; set; }
    public Guid? UserId { get; set; }          // null if anonymous
    public string? IpAddress { get; set; }      // for anonymous tracking
    public string? SessionId { get; set; }

    // Navigation
    public Poll Poll { get; set; } = null!;
    public PollOption PollOption { get; set; } = null!;
    // User navigation intentionally omitted — AppUser lives in Domain without Identity
}
