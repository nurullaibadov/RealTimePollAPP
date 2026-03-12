using RealTimePoll.Domain.Enums;

namespace RealTimePoll.Application.DTOs.Poll;

// ── Request DTOs ──────────────────────────────────────────────────────────────

public record CreatePollRequest(
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime EndsAt,
    bool AllowMultipleVotes,
    bool IsAnonymous,
    string? ThumbnailUrl,
    List<CreatePollOptionRequest> Options
);

public record CreatePollOptionRequest(
    string Text,
    string? ImageUrl,
    int OrderIndex
);

public record UpdatePollRequest(
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime EndsAt,
    bool AllowMultipleVotes,
    bool IsAnonymous,
    string? ThumbnailUrl
);

public record PollFilterRequest
{
    public string? Search { get; init; }
    public PollStatus? Status { get; init; }
    public bool? IsActive { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public string SortBy { get; init; } = "createdAt";
    public bool SortDesc { get; init; } = true;
}

// ── Response DTOs ─────────────────────────────────────────────────────────────

public record PollResponse(
    Guid Id,
    string Title,
    string? Description,
    DateTime StartsAt,
    DateTime EndsAt,
    bool IsActive,
    bool AllowMultipleVotes,
    bool IsAnonymous,
    PollStatus Status,
    string? ThumbnailUrl,
    int TotalVotes,
    string CreatedByUserName,
    DateTime CreatedAt,
    List<PollOptionResponse> Options
);

public record PollOptionResponse(
    Guid Id,
    string Text,
    string? ImageUrl,
    int VoteCount,
    int OrderIndex,
    double Percentage
);

public record PollSummaryResponse(
    Guid Id,
    string Title,
    PollStatus Status,
    bool IsActive,
    int TotalVotes,
    DateTime EndsAt,
    DateTime CreatedAt,
    int OptionCount
);

public record PagedResult<T>(
    IEnumerable<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages
);

public record PollStatsResponse(
    int TotalPolls,
    int ActivePolls,
    int TotalVotes,
    int TotalUsers,
    List<PollSummaryResponse> RecentPolls
);
