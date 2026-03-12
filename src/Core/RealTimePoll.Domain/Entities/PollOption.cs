namespace RealTimePoll.Domain.Entities;

public class PollOption : BaseEntity
{
    public string Text { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public int VoteCount { get; set; } = 0;
    public int OrderIndex { get; set; } = 0;

    // Foreign Keys
    public Guid PollId { get; set; }

    // Navigation
    public Poll Poll { get; set; } = null!;
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
