namespace RealTimePoll.Application.DTOs.Vote;

public record CastVoteRequest(
    Guid PollId,
    List<Guid> OptionIds  // supports multiple if AllowMultipleVotes
);

public record VoteResultResponse(
    Guid PollId,
    string PollTitle,
    int TotalVotes,
    List<OptionResultResponse> Results
);

public record OptionResultResponse(
    Guid OptionId,
    string OptionText,
    int VoteCount,
    double Percentage
);

public record RealTimeVoteUpdate(
    Guid PollId,
    Guid OptionId,
    int NewVoteCount,
    int TotalVotes,
    double Percentage
);
