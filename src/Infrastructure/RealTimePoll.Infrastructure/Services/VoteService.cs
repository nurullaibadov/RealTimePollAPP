using Microsoft.EntityFrameworkCore;
using RealTimePoll.Application.DTOs.Vote;
using RealTimePoll.Application.Interfaces;
using RealTimePoll.Domain.Entities;
using RealTimePoll.Domain.Interfaces;
using RealTimePoll.Infrastructure.Persistence.Context;

namespace RealTimePoll.Infrastructure.Services;

public class VoteService : IVoteService
{
    private readonly IUnitOfWork _uow;
    private readonly AppDbContext _context;

    public VoteService(IUnitOfWork uow, AppDbContext context)
    {
        _uow = uow;
        _context = context;
    }

    public async Task<VoteResultResponse> CastVoteAsync(
        CastVoteRequest request, Guid? userId, string? ipAddress, string? sessionId)
    {
        var poll = await _context.Polls
            .Include(p => p.Options)
            .FirstOrDefaultAsync(p => p.Id == request.PollId && !p.IsDeleted)
            ?? throw new KeyNotFoundException("Anket bulunamadı.");

        if (!poll.IsActive)
            throw new InvalidOperationException("Bu anket aktif değil.");

        if (poll.EndsAt < DateTime.UtcNow)
            throw new InvalidOperationException("Bu anketin süresi dolmuştur.");

        if (poll.StartsAt > DateTime.UtcNow)
            throw new InvalidOperationException("Bu anket henüz başlamamıştır.");

        // Check duplicate vote
        var alreadyVoted = await HasUserVotedAsync(request.PollId, userId, ipAddress);
        if (alreadyVoted)
            throw new InvalidOperationException("Bu ankette zaten oy kullandınız.");

        // Validate options
        if (!poll.AllowMultipleVotes && request.OptionIds.Count > 1)
            throw new InvalidOperationException("Bu anket tek seçenek oyuna izin vermektedir.");

        var validOptionIds = poll.Options.Select(o => o.Id).ToHashSet();
        var invalidOptions = request.OptionIds.Where(id => !validOptionIds.Contains(id)).ToList();
        if (invalidOptions.Any())
            throw new InvalidOperationException("Geçersiz seçenek ID'si.");

        await _uow.BeginTransactionAsync();
        try
        {
            foreach (var optionId in request.OptionIds)
            {
                var vote = new Vote
                {
                    PollId = request.PollId,
                    PollOptionId = optionId,
                    UserId = userId,
                    IpAddress = ipAddress,
                    SessionId = sessionId
                };
                await _uow.Votes.AddAsync(vote);

                // Increment option vote count
                var option = poll.Options.First(o => o.Id == optionId);
                option.VoteCount++;
                _uow.PollOptions.Update(option);
            }

            poll.TotalVotes++;
            _uow.Polls.Update(poll);

            await _uow.SaveChangesAsync();
            await _uow.CommitTransactionAsync();
        }
        catch
        {
            await _uow.RollbackTransactionAsync();
            throw;
        }

        return await GetResultsAsync(request.PollId);
    }

    public async Task<VoteResultResponse> GetResultsAsync(Guid pollId)
    {
        var poll = await _context.Polls
            .Include(p => p.Options.Where(o => !o.IsDeleted))
            .FirstOrDefaultAsync(p => p.Id == pollId && !p.IsDeleted)
            ?? throw new KeyNotFoundException("Anket bulunamadı.");

        var results = poll.Options
            .OrderByDescending(o => o.VoteCount)
            .Select(o => new OptionResultResponse(
                o.Id, o.Text, o.VoteCount,
                poll.TotalVotes > 0 ? Math.Round((double)o.VoteCount / poll.TotalVotes * 100, 1) : 0
            )).ToList();

        return new VoteResultResponse(poll.Id, poll.Title, poll.TotalVotes, results);
    }

    public async Task<bool> HasUserVotedAsync(Guid pollId, Guid? userId, string? ipAddress)
    {
        if (userId.HasValue)
        {
            return await _uow.Votes.AnyAsync(v =>
                v.PollId == pollId && v.UserId == userId && !v.IsDeleted);
        }

        if (!string.IsNullOrWhiteSpace(ipAddress))
        {
            return await _uow.Votes.AnyAsync(v =>
                v.PollId == pollId && v.IpAddress == ipAddress && !v.IsDeleted);
        }

        return false;
    }
}
